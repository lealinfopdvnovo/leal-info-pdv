using LealInfo.IFood.Groceries;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

static class ReceptionTests
{
    public static async Task<int> Run()
    {
        int passed=0;
        void Check(bool ok,string label){if(!ok)throw new Exception("FAIL reception: "+label);Console.WriteLine("PASS reception: "+label);passed++;}
        async Task<bool> Reject(Func<Task> action){try{await action();return false;}catch(ReceptionFailure){return true;}}
        var merchant=Guid.NewGuid();var order=Guid.NewGuid();var eventId=Guid.NewGuid();
        var now=new DateTimeOffset(2026,10,26,20,0,0,TimeSpan.Zero);
        var eventTime=now;
        var root=Path.Combine(Path.GetTempPath(),"ifood-reception-"+Guid.NewGuid());Directory.CreateDirectory(root);
        string Fixture(Guid? id=null,string code="PLC",string full="PLACED",DateTimeOffset? at=null)=>JsonSerializer.Serialize(new[]{new{
            id=id??eventId,orderId=order,merchantId=merchant,code,fullCode=full,createdAt=(at??eventTime).ToString("O"),
            metadata=new{privateCustomer="DO_NOT_STORE",clientSecret="NOT_A_REAL_SECRET"}}});
        HttpResponseMessage Response(int code,string body="")=>new((HttpStatusCode)code){Content=new StringContent(body)};
        string NewDir(string name)=>Path.Combine(root,name);
        var logs=new List<AuthenticationEvidence>();string secret="ISOLATED_FAKE_TOKEN_DO_NOT_EXPORT";
        try
        {
            var dir=NewDir("normal");int get=0,post=0;
            using(var store=new EventJournal(dir,merchant))
            using(var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(async request=>{
                Check(request.RequestUri!.Host=="merchant-api.ifood.com.br"&&request.Headers.Authorization?.Parameter==secret,"official host and bearer header");
                if(request.Method==HttpMethod.Get){
                    get++;Check(request.RequestUri.AbsolutePath==EventsClient.PollRoute&&request.RequestUri.Query=="","Events route without automatic-ACK filters");
                    Check(request.Headers.GetValues("x-polling-merchants").Single()==merchant.ToString(),"single test merchant filter");
                    return Response(200,Fixture());
                }
                post++;var text=await request.Content!.ReadAsStringAsync();var ids=JsonNode.Parse(text)!.AsArray();
                Check(request.RequestUri.AbsolutePath==EventsClient.AckRoute&&ids.Count==1&&ids[0]!["id"]!.GetValue<string>()==eventId.ToString()&&ids[0]!.AsObject().Count==1,"ACK payload contains unique event ID only");
                Check(File.ReadAllText(Path.Combine(dir,"events-v1.json")).Contains(eventId.ToString()),"journal persisted before remote ACK");
                return Response(202);
            })))
            {
                var receiver=new EventReceiver(api,store,()=>now);
                var cycles=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>receiver.RunCycleAsync(default)));
                Check(get==1&&post==1&&cycles.Count(c=>c.Polled)==1,"concurrent cycles perform one poll/ACK");
                Check(store.Snapshot().Single().AckAccepted&&!store.Snapshot().Single().BusinessProcessed,"transport ACK is never business confirmation");
                now=now.AddSeconds(29);await receiver.RunCycleAsync(default);Check(get==1,"no polling before 30 seconds");
                now=now.AddSeconds(1);var duplicate=await receiver.RunCycleAsync(default);
                Check(duplicate.Added==0&&duplicate.Received==1&&store.Snapshot().Count==1&&post==2,"redelivery ACK repeated without duplicate order/event");
                bool locked=false;try{using var second=new EventJournal(dir,merchant);}catch(IOException){locked=true;}Check(locked,"second receiver instance blocked by exclusive journal lease");
            }
            using(var store=new EventJournal(dir,merchant))
            using(var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(_=>throw new Exception("Unexpected HTTP"))))
            {
                var cycle=await new EventReceiver(api,store,()=>now).RunCycleAsync(default);
                Check(!cycle.Polled&&store.Snapshot().Count==1&&store.PendingIds().Count==0,"restart retains events, accepted ACK and 30-second throttle");
            }
            string journalText=File.ReadAllText(Path.Combine(dir,"events-v1.json"));
            Check(!journalText.Contains("DO_NOT_STORE")&&!journalText.Contains("NOT_A_REAL_SECRET")&&!journalText.Contains(secret),"journal stores allowlisted technical event fields only");
            Check(!JsonSerializer.Serialize(logs).Contains(secret)&&logs.Any(l=>l.Status==202),"HTTP evidence excludes bearer and body");

            foreach(int code in new[]{200,204})
            {
                int requests=0;using var store=new EventJournal(NewDir("empty"+code),merchant);
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(_=>{requests++;return Task.FromResult(Response(code,code==200?"[]":""));}));
                var c=await new EventReceiver(api,store,()=>now).RunCycleAsync(default);
                Check(c.Polled&&c.Received==0&&requests==1&&store.Snapshot().Count==0,"HTTP "+code+" empty is connection evidence only, without ACK");
            }
            foreach(int code in new[]{400,401,403,404,302})
            {
                int requests=0;using var store=new EventJournal(NewDir("fatal"+code),merchant);
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(_=>{requests++;return Task.FromResult(Response(code,secret));}));
                var receiver=new EventReceiver(api,store,()=>now);
                Check(await Reject(async()=>{await receiver.RunCycleAsync(default);})&&receiver.Stopped&&requests==1,"HTTP "+code+" stops without retry or endpoint fallback");
                Check(await Reject(async()=>{await receiver.RunCycleAsync(default);})&&requests==1,"stopped receiver cannot silently retry HTTP "+code);
            }
            {
                int calls=0;using var store=new EventJournal(NewDir("retry"),merchant);
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(_=>{calls++;var r=Response(429);r.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(90));return Task.FromResult(r);}));
                var receiver=new EventReceiver(api,store,()=>now);await receiver.RunCycleAsync(default);
                Check(store.NextAttemptUtc==now.AddSeconds(90)&&calls==1,"429 honors Retry-After and minimum backoff");
                now=now.AddSeconds(89);await receiver.RunCycleAsync(default);Check(calls==1,"no request during Retry-After");
                now=now.AddSeconds(1);await receiver.RunCycleAsync(default);Check(store.NextAttemptUtc==now.AddSeconds(120),"second transient failure backs off 120 seconds");
                now=now.AddSeconds(120);Check(await Reject(async()=>{await receiver.RunCycleAsync(default);})&&receiver.Stopped&&calls==3,"third consecutive failure stops instead of infinite retries");
            }
            {
                using var store=new EventJournal(NewDir("long-retry"),merchant);
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(_=>{
                    var response=Response(429);response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(2));return Task.FromResult(response);
                }));
                await new EventReceiver(api,store,()=>now).RunCycleAsync(default);
                Check(store.NextAttemptUtc==now.AddHours(2),"Retry-After longer than one hour is not shortened");
            }
            {
                var recoveryDir=NewDir("ack-recovery");int ack=0,getCount=0;
                using(var store=new EventJournal(recoveryDir,merchant))
                using(var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(req=>{
                    if(req.Method==HttpMethod.Get){getCount++;return Task.FromResult(Response(200,Fixture()));}
                    ack++;return Task.FromResult(Response(500,secret));
                })))
                {
                    await new EventReceiver(api,store,()=>now).RunCycleAsync(default);
                    Check(store.PendingIds().Count==1&&!store.Snapshot().Single().AckAccepted,"failed ACK keeps persisted event pending");
                }
                now=now.AddSeconds(60);
                using(var store=new EventJournal(recoveryDir,merchant))
                using(var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(req=>{
                    if(req.Method==HttpMethod.Post){ack++;return Task.FromResult(Response(202));}
                    getCount++;Check(store.PendingIds().Count==0,"restart retries pending ACK before polling");return Task.FromResult(Response(204));
                })))
                {
                    await new EventReceiver(api,store,()=>now).RunCycleAsync(default);
                    Check(ack==2&&getCount==2&&store.Snapshot().Count==1&&store.PendingIds().Count==0,"restart recovers failed ACK with no duplicate event");
                }
            }
            var invalid = new Dictionary<string,Action<JsonNode>> {
                ["foreign merchant"]=n=>n[0]!["merchantId"]=Guid.NewGuid().ToString(),
                ["missing merchant"]=n=>n[0]!.AsObject().Remove("merchantId"),
                ["empty ID"]=n=>n[0]!["id"]=Guid.Empty.ToString(),
                ["missing timestamp"]=n=>n[0]!.AsObject().Remove("createdAt"),
                ["timestamp without timezone"]=n=>n[0]!["createdAt"]="2026-10-26T12:00:00",
                ["log injection"]=n=>n[0]!["code"]="PLC\\nFAKE",
                ["null event"]=n=>n[0]=null
            };
            foreach(var test in invalid)
            {
                var json=JsonNode.Parse(Fixture())!;test.Value(json);int calls=0;
                using var store=new EventJournal(NewDir("invalid"+passed),merchant);
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(_=>{calls++;return Task.FromResult(Response(200,json.ToJsonString()));}));
                Check(await Reject(async()=>{await new EventReceiver(api,store,()=>now).RunCycleAsync(default);})&&calls==1&&store.Snapshot().Count==0,test.Key+" rejects entire batch without ACK");
            }
            foreach(string bad in new[]{"{}","null","","[] {}",new string(' ',2000001),Fixture().Replace("\"code\":\"PLC\"","\"code\":\"PLC\",\"code\":\"CAN\"")})
            {
                using var store=new EventJournal(NewDir("bad"+passed),merchant);int calls=0;
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(_=>{calls++;return Task.FromResult(Response(200,bad));}));
                Check(await Reject(async()=>{await new EventReceiver(api,store,()=>now).RunCycleAsync(default);})&&calls==1,"malformed/oversized/duplicate-key response rejected without ACK");
            }
            {
                using var store=new EventJournal(NewDir("conflict"),merchant);int gets=0,acks=0;
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(req=>{
                    if(req.Method==HttpMethod.Post){acks++;return Task.FromResult(Response(202));}
                    var body=Fixture(at:now);if(++gets==2)body=body.Replace(order.ToString(),Guid.NewGuid().ToString());return Task.FromResult(Response(200,body));
                }));
                var receiver=new EventReceiver(api,store,()=>now);await receiver.RunCycleAsync(default);now=now.AddSeconds(30);
                Check(await Reject(async()=>{await receiver.RunCycleAsync(default);})&&acks==1&&store.Snapshot().Count==1,"conflicting event identity stops with no second ACK");
            }
            {
                using var store=new EventJournal(NewDir("sorted"),merchant);
                var newer=JsonNode.Parse(Fixture(Guid.NewGuid(),"RFI","READY_FOR_INVOICE",now.AddMinutes(1)))![0]!.DeepClone();
                var older=JsonNode.Parse(Fixture(Guid.NewGuid(),"XYZ","UNKNOWN_FUTURE_EVENT",now))![0]!.DeepClone();
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(req=>Task.FromResult(Response(req.Method==HttpMethod.Get?200:202,req.Method==HttpMethod.Get?new JsonArray(newer,older).ToJsonString():""))));
                await new EventReceiver(api,store,()=>now).RunCycleAsync(default);
                Check(store.Snapshot()[0].Code=="XYZ"&&store.Snapshot()[1].AwaitingFinalBag,"out-of-order events sorted; unknown event retained; RFI remains pending final import");
                Check(store.Snapshot().All(e=>e.AckAccepted&&!e.BusinessProcessed),"all technical events acknowledged without pretending business processing");
            }
            {
                var diskDir=NewDir("disk-failure");using var store=new EventJournal(diskDir,merchant);int acks=0;
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(req=>{
                    if(req.Method==HttpMethod.Post){acks++;return Task.FromResult(Response(202));}
                    Directory.CreateDirectory(Path.Combine(diskDir,"events-v1.json.tmp"));return Task.FromResult(Response(200,Fixture()));
                }));
                bool failed=false;try{await new EventReceiver(api,store,()=>now).RunCycleAsync(default);}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
                Check(failed&&acks==0&&store.Snapshot().Count==0,"disk write failure prevents ACK and in-memory publication");
            }
            {
                var corrupt=NewDir("corrupt");Directory.CreateDirectory(corrupt);File.WriteAllText(Path.Combine(corrupt,"events-v1.json"),"broken");
                bool failed=false;try{using var store=new EventJournal(corrupt,merchant);}catch(ReceptionFailure){failed=true;}
                Check(failed&&File.ReadAllText(Path.Combine(corrupt,"events-v1.json"))=="broken","corrupt journal fails closed and is preserved");
            }
            {
                using var store=new EventJournal(NewDir("cancel"),merchant);int calls=0;using var cts=new CancellationTokenSource();cts.Cancel();
                using var api=new EventsClient(merchant,_=>Task.FromResult(secret),logs.Add,new Stub(_=>{calls++;return Task.FromResult(Response(204));}));
                bool cancelled=false;try{await new EventReceiver(api,store,()=>now).RunCycleAsync(cts.Token);}catch(OperationCanceledException){cancelled=true;}
                Check(cancelled&&calls==0,"user cancellation prevents polling");
            }
        }
        finally{Directory.Delete(root,true);}
        Console.WriteLine("RECEPTION TOTAL "+passed+" PASS; fake HTTP and isolated disk only.");return passed;
    }
}
