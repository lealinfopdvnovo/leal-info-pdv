using LealInfo.IFood.Groceries;
using System.Net;
using System.Text.Json;

static class CentralizedTests
{
    public static async Task<int> Run()
    {
        int passed=0;
        void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);Console.WriteLine("PASS centralized: "+label);passed++;}
        async Task<bool> Reject(Func<Task> action){try{await action();return false;}catch(AuthenticationFailure){return true;}}
        string key=Guid.NewGuid().ToString(),token=Guid.NewGuid().ToString();var merchant=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        var logs=new List<AuthenticationEvidence>();var paths=new List<string>();int tokenCalls=0;
        string Token()=>JsonSerializer.Serialize(new{accessToken=token,type="bearer",expiresIn=60});
        using(var auth=new AuthenticationClient("test-C",key,logs.Add,new Stub(async req=>{
            paths.Add(req.RequestUri!.AbsolutePath);
            if(req.Method==HttpMethod.Post){
                tokenCalls++;var body=await req.Content!.ReadAsStringAsync();
                Check(req.RequestUri.AbsoluteUri=="https://merchant-api.ifood.com.br/authentication/v1.0/oauth/token" && body=="grantType=client_credentials&clientId=test-C&clientSecret="+key,"client_credentials sent only to official token endpoint");
                Check(!body.Contains("authorizationCode")&&!body.Contains("refreshToken"),"no partner code or refresh grant required");
                return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Token())};
            }
            Check(req.RequestUri.AbsolutePath=="/merchant/v1.0/merchants/"+merchant && req.Headers.Authorization?.Parameter==token,"query only selected test merchant with bearer");
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{id=merchant}))};
        }),()=>now,AuthenticationMode.Centralized)){
            Check(await Reject(async()=>{await auth.BeginAsync(default);})&&paths.Count==0,"centralized app never requests partner userCode");
            Check(await Reject(()=>auth.CompleteAsync(null!,"unused",default))&&paths.Count==0,"partner authorization flow rejected locally");
            await auth.ConnectCentralizedAsync(default);
            Check(await auth.GetAccessTokenAsync(default)==token,"token accepted without refreshToken in response");
            await Task.WhenAll(Enumerable.Range(0,12).Select(_=>auth.ConnectCentralizedAsync(default)));
            Check(tokenCalls==1,"repeat connect reuses valid token without API calls");
            await auth.VerifyMerchantAsync(merchant,default);now=now.AddSeconds(60);
            await Task.WhenAll(Enumerable.Range(0,12).Select(_=>auth.GetAccessTokenAsync(default)));
            Check(tokenCalls==2,"expiry triggers one client_credentials renewal across concurrent calls");
        }
        var serialized=JsonSerializer.Serialize(logs);
        Check(!serialized.Contains(key)&&!serialized.Contains(token),"centralized evidence excludes secrets");
        foreach(int status in new[]{401,403,429,500}){
            int calls=0;using var auth=new AuthenticationClient("test-C",key,_=>{},new Stub(_=>{calls++;return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status){Content=new StringContent(key)});}),null,AuthenticationMode.Centralized);
            Check(await Reject(()=>auth.ConnectCentralizedAsync(default))&&calls==1,"HTTP "+status+" no automatic retry or grant fallback");
            Check(await Reject(async()=>{await auth.GetAccessTokenAsync(default);})&&calls==1,"failed centralized login not silently retried");
        }
        using(var auth=new AuthenticationClient("test-D",key,_=>{},new Stub(_=>throw new Exception("Must not call HTTP"))))
            Check(await Reject(()=>auth.ConnectCentralizedAsync(default)),"distributed credentials mode cannot use centralized grant accidentally");
        Console.WriteLine("CENTRALIZED TOTAL "+passed+" PASS; fake HTTP only.");return passed;
    }
}
