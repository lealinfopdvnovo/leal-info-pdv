using LealInfo.IFood.Groceries;
using System.Net;
using System.Text.Json;

static class PortableBagTests
{
    public static async Task<int> Run()
    {
        int passed=0;void Check(bool ok,string label){if(!ok)throw new Exception("FAIL portable bag: "+label);Console.WriteLine("PASS portable bag: "+label);passed++;}
        async Task<bool> Reject(Func<Task> action){try{await action();return false;}catch(ReceptionFailure){return true;}}
        var merchant=Guid.NewGuid();var order=Guid.NewGuid();var eid=Guid.NewGuid();var ids=new[]{Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()};
        object Money(int cents)=>new{value=cents,currency="BRL"};
        string Fixture()=>JsonSerializer.Serialize(new{id=order,merchant=new{id=merchant},test=true,category="GROCERY",customer=new{name="DO_NOT_STORE_CUSTOMER",phone="PRIVATE_PHONE"},
            bag=new{items=Enumerable.Range(0,3).Select(i=>new{uniqueId=ids[i],name="Teste produto "+i,quantity=i==2?1:3,unavailable=i==0,product=new{isVariableWeight=false},prices=new{unitValue=Money(1000),grossValue=Money(i==2?1000:3000)}}),prices=new{grossValue=Money(4000)}},
            operationMode=new{type="DELIVERY",schedulingType="IMMEDIATE",delivery=new{}},payment=new{methods=new[]{new{amount=Money(3500)}}},
            benefit=new{benefits=new[]{new{target="CART",sponsorships=new[]{new{liability="OWN",amount=Money(500)}}}}}});
        HttpResponseMessage Reply(int status,string body="")=>new((HttpStatusCode)status){Content=new StringContent(body)};
        async Task Stage(EventJournal journal,Guid id,string code="RFI",string full="READY_FOR_INVOICE")
        {
            string json=JsonSerializer.Serialize(new[]{new{id,orderId=order,merchantId=merchant,code,fullCode=full,createdAt="2026-10-26T20:00:00Z"}});
            using var api=new EventsClient(merchant,_=>Task.FromResult("fake"),_=>{},new Stub(_=>Task.FromResult(Reply(200,json))));
            journal.Stage(await api.PollAsync(default));
        }
        var root=Path.Combine(Path.GetTempPath(),"ifood-bags-"+Guid.NewGuid());Directory.CreateDirectory(root);
        string Dir(string name)=>Path.Combine(root,name);
        var logs=new List<AuthenticationEvidence>();string token="FAKE_TOKEN_PORTABLE_BAG";
        try
        {
            using(var journal=new EventJournal(Dir("normal"),merchant))using(var store=new BagStore(Dir("normal"),merchant))
            {
                int calls=0;using var processor=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(request=>{
                    calls++;Check(request.Method==HttpMethod.Get && request.RequestUri!.AbsoluteUri=="https://merchant-api.ifood.com.br/order/v1.0/orders/"+order+"/virtual-bag","Windows reader uses official fixed virtual-bag GET route");
                    Check(request.Headers.Authorization?.Parameter==token,"bearer is sent only in request header");return Task.FromResult(Reply(200,Fixture()));
                }));
                await Stage(journal,Guid.NewGuid(),"PLC","PLACED");Check(await processor.ProcessAsync(default)==0&&calls==0,"no fetch before RFI");
                await Stage(journal,eid);
                var runs=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>processor.ProcessAsync(default)));
                Check(runs.Sum()==1&&calls==1&&store.Snapshot().Count==1,"twelve concurrent processors import one bag");
                var bag=store.Snapshot().Single();
                Check(bag.Items.Select(i=>i.AvailableQuantity).SequenceEqual(new[]{0,3,1}),"portable unavailable logic preserves final 0/3/1 quantities");
                Check(bag.Items[0].ReportedQuantity==3&&bag.Items[0].GrossCents==3000,"cancelled line preserves source quantity/value without reintroducing available stock");
                Check(bag.PaymentCents==3500&&bag.BagGrossCents==4000&&bag.Benefits.Single().Cents==500,"coupon sponsorship, payment and bag amounts remain separate");
                Check(journal.Snapshot().Single(e=>e.Id==eid.ToString()).BusinessProcessed&&!journal.Snapshot().Single(e=>e.Code=="PLC").BusinessProcessed,"only persisted final bag marks its own RFI checkpoint");
                bag.Items[0]=bag.Items[1];Check(store.Snapshot().Single().Items[0].Id==ids[0],"store snapshots are deep copies");
            }
            using(var journal=new EventJournal(Dir("normal"),merchant))using(var store=new BagStore(Dir("normal"),merchant))
            using(var processor=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(_=>throw new Exception("Should not fetch on restart"))))
                Check(await processor.ProcessAsync(default)==0&&store.Snapshot().Count==1,"restart loads normalized bag and avoids refetch");
            var data=File.ReadAllText(Path.Combine(Dir("normal"),"final-bags-v1.json"));
            Check(!data.Contains("DO_NOT_STORE_CUSTOMER")&&!data.Contains("PRIVATE_PHONE")&&!data.Contains(token),"persisted bag excludes consumer identity and credentials");
            Check(!JsonSerializer.Serialize(logs).Contains(token)&&logs.Single().Status==200,"HTTP evidence excludes token and order body");
            using(var journal=new EventJournal(Dir("crash"),merchant))using(var store=new BagStore(Dir("crash"),merchant))
            {
                await Stage(journal,eid);store.Save(eid,PortableBag.Parse(Fixture(),order,merchant));
                Check(!journal.Snapshot().Single().BusinessProcessed,"simulated crash leaves event checkpoint pending after bag persistence");
            }
            using(var journal=new EventJournal(Dir("crash"),merchant))using(var store=new BagStore(Dir("crash"),merchant))
            using(var processor=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(_=>throw new Exception("Crash recovery must not refetch"))))
            {Check(await processor.ProcessAsync(default)==0&&journal.Snapshot().Single().BusinessProcessed,"restart repairs missing event checkpoint using durable bag");}
            foreach(int status in new[]{401,403,404,429,500,302})
            {
                using var journal=new EventJournal(Dir("status"+status),merchant);using var store=new BagStore(Dir("status"+status),merchant);await Stage(journal,eid);int calls=0;
                using var processor=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(_=>{calls++;return Task.FromResult(Reply(status,token));}));
                Check(await Reject(async()=>{await processor.ProcessAsync(default);})&&calls==1&&store.Snapshot().Count==0&&!journal.Snapshot().Single().BusinessProcessed,"HTTP "+status+" preserves pending RFI and never silently retries");
            }
            var variants=new[]{
                Fixture().Replace("\"unavailable\":true","\"unavailable\":\"true\""),
                Fixture().Replace("\"unavailable\":true,",""),
                Fixture().Replace("\"test\":true","\"test\":false"),
                Fixture().Replace(merchant.ToString(),Guid.NewGuid().ToString()),
                Fixture().Replace("\"test\":true","\"test\":true,\"test\":true"),
                Fixture()+" {}",new string(' ',1048577)
            };
            for(int n=0;n<variants.Length;n++)
            {
                using var journal=new EventJournal(Dir("invalid"+n),merchant);using var store=new BagStore(Dir("invalid"+n),merchant);await Stage(journal,eid);
                using var processor=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(_=>Task.FromResult(Reply(200,variants[n]))));
                Check(await Reject(async()=>{await processor.ProcessAsync(default);})&&store.Snapshot().Count==0&&!journal.Snapshot().Single().BusinessProcessed,"malformed/production/foreign/oversized bag rejected without persistence");
            }
            using(var journal=new EventJournal(Dir("disk"),merchant))using(var store=new BagStore(Dir("disk"),merchant))
            {
                await Stage(journal,eid);Directory.CreateDirectory(Path.Combine(Dir("disk"),"final-bags-v1.json.tmp"));
                using var processor=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(_=>Task.FromResult(Reply(200,Fixture()))));
                bool failed=false;try{await processor.ProcessAsync(default);}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
                Check(failed&&store.Snapshot().Count==0&&!journal.Snapshot().Single().BusinessProcessed,"disk failure leaves final bag checkpoint pending");
            }
            using(var journal=new EventJournal(Dir("guard"),merchant))
            {
                await Stage(journal,eid,"CAN","CANCELLED");
                Check(await Reject(()=>{journal.MarkBagPersisted(eid.ToString());return Task.CompletedTask;}),"non-RFI event cannot be marked as final bag persisted");
            }
            using(var journal=new EventJournal(Dir("lost-store"),merchant))using(var store=new BagStore(Dir("lost-store"),merchant))
            {
                await Stage(journal,eid);journal.MarkBagPersisted(eid.ToString());int calls=0;
                using var processor=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(_=>{calls++;return Task.FromResult(Reply(200,Fixture()));}));
                Check(await processor.ProcessAsync(default)==1&&calls==1&&store.Snapshot().Count==1,"missing bag file is recovered even when event checkpoint previously existed");
            }
            using(var journal=new EventJournal(Dir("second-commit"),merchant))using(var store=new BagStore(Dir("second-commit"),merchant))
            {
                await Stage(journal,eid);
                using(var processor=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(_=>{
                    Directory.CreateDirectory(Path.Combine(Dir("second-commit"),"events-v1.json.tmp"));return Task.FromResult(Reply(200,Fixture()));
                })))
                {
                    bool failed=false;try{await processor.ProcessAsync(default);}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
                    Check(failed&&store.Snapshot().Count==1&&!journal.Snapshot().Single().BusinessProcessed,"second commit disk failure preserves bag without false checkpoint");
                }
                Directory.Delete(Path.Combine(Dir("second-commit"),"events-v1.json.tmp"));
                using var recovery=new BagProcessor(journal,store,_=>Task.FromResult(token),logs.Add,new Stub(_=>throw new Exception("Must reuse persisted bag")));
                Check(await recovery.ProcessAsync(default)==0&&journal.Snapshot().Single().BusinessProcessed,"recovery repairs second-commit failure without another GET");
            }
            {
                string corrupt=Dir("corrupt-bag");Directory.CreateDirectory(corrupt);File.WriteAllText(Path.Combine(corrupt,"final-bags-v1.json"),"broken");
                bool failed=false;try{using var store=new BagStore(corrupt,merchant);}catch(ReceptionFailure){failed=true;}
                Check(failed&&File.ReadAllText(Path.Combine(corrupt,"final-bags-v1.json"))=="broken","corrupt bag store fails closed without overwriting evidence");
            }
        }
        finally{Directory.Delete(root,true);}
        Console.WriteLine("PORTABLE BAG TOTAL "+passed+" PASS; isolated fake HTTP and files only.");return passed;
    }
}
