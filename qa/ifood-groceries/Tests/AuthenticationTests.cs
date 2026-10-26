using LealInfo.IFood.Groceries;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

static class AuthenticationTests
{
    public static async Task<int> Run()
    {
        int passed=0;
        void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);Console.WriteLine("PASS auth: "+label);passed++;}
        async Task<bool> Reject(Func<Task> action){try{await action();return false;}catch(AuthenticationFailure){return true;}}
        string secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(20)), access=Guid.NewGuid().ToString(), refresh=Guid.NewGuid().ToString(), verifier=Guid.NewGuid().ToString();
        var instant=DateTimeOffset.UtcNow; var merchant=Guid.NewGuid();var logs=new List<AuthenticationEvidence>();int requests=0, renewals=0;
        string Code()=>JsonSerializer.Serialize(new{userCode="TEST-CODE",authorizationCodeVerifier=verifier,verificationUrl="https://portal.ifood.com.br/apps/code",verificationUrlComplete="https://portal.ifood.com.br/apps/code?c=TEST-CODE",expiresIn=600});
        string Token(int seconds=60)=>JsonSerializer.Serialize(new{accessToken=access,refreshToken=refresh,type="bearer",expiresIn=seconds});
        HttpResponseMessage Reply(string body,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(body)};
        async Task<HttpResponseMessage> Handle(HttpRequestMessage request)
        {
            requests++;Check(request.RequestUri!.Host=="merchant-api.ifood.com.br" && request.RequestUri.Scheme=="https","fixed HTTPS API origin");
            var path=request.RequestUri.AbsolutePath;
            if(path.EndsWith("/userCode")){
                Check(request.Method==HttpMethod.Post && await request.Content!.ReadAsStringAsync()=="clientId=test-client","link code sends client ID only as form");return Reply(Code());
            }
            if(path.EndsWith("/token")){
                var body=await request.Content!.ReadAsStringAsync();
                Check(request.Content.Headers.ContentType!.MediaType=="application/x-www-form-urlencoded" && body.Contains("clientSecret="+secret),"token credentials in form body only");
                if(body.Contains("grantType=refresh_token")){renewals++;Check(body.Contains("refreshToken="+refresh),"refresh token grant contract");}
                else Check(body.Contains("grantType=authorization_code") && body.Contains("authorizationCode=ABC%2B123%26") && body.Contains("authorizationCodeVerifier="+verifier),"authorization and verifier are URL encoded");
                return Reply(Token());
            }
            Check(request.Method==HttpMethod.Get && path=="/merchant/v1.0/merchants/"+merchant,"only specified merchant queried");
            Check(request.Headers.Authorization?.Parameter==access,"bearer confined to API header");return Reply(JsonSerializer.Serialize(new{id=merchant}));
        }
        using(var auth=new DistributedAuthentication("test-client",secret,logs.Add,new Stub(Handle),()=>instant)){
            Check(await Reject(async()=>{await auth.GetAccessTokenAsync(default);}),"no token before authorization");
            var code=await auth.BeginAsync(default);
            Check(code.VerificationUrl.AbsoluteUri=="https://portal.ifood.com.br/apps/code?c=TEST-CODE" && !code.ToString().Contains(verifier),"safe portal URL and private ToString");
            await auth.CompleteAsync(code,"ABC+123&",default);
            int before=requests;
            var tokens=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>auth.GetAccessTokenAsync(default)));
            Check(tokens.All(t=>t==access) && requests==before,"valid token reused for 12 calls without HTTP");
            Check(await Reject(()=>auth.CompleteAsync(code,"ABC+123&",default)) && requests==before,"consumed authorization not replayed");
            await auth.VerifyMerchantAsync(merchant,default);
            instant=instant.AddSeconds(60);
            await Task.WhenAll(Enumerable.Range(0,12).Select(_=>auth.GetAccessTokenAsync(default)));
            Check(renewals==1,"expiration from expiresIn; concurrent calls refresh once");
        }
        string evidence=JsonSerializer.Serialize(logs);
        Check(new[]{secret,access,refresh,verifier,"ABC+123&",merchant.ToString()}.All(s=>!evidence.Contains(s)),"logs exclude credentials and merchant identifier");
        foreach(var status in new[]{302,400,401,403,429,500}){
            int count=0;using var auth=new DistributedAuthentication("id",secret,_=>{},new Stub(_=>{count++;return Task.FromResult(Reply(secret,(HttpStatusCode)status));}));
            string message="";try{await auth.BeginAsync(default);}catch(AuthenticationFailure e){message=e.Message;Check(e.Status==status,"preserves HTTP status "+status);}
            Check(count==1 && !message.Contains(secret),"HTTP "+status+" stops once without exposing response");
        }
        foreach(var url in new[]{"https://portal.ifood.com.br.evil.example/apps/code","http://portal.ifood.com.br/apps/code","https://portal.ifood.com.br/apps/code#x","https://portal.ifood.com.br:444/apps/code","https://user@portal.ifood.com.br/apps/code"}){
            var n=JsonNode.Parse(Code())!;n["verificationUrlComplete"]=url;
            using var auth=new DistributedAuthentication("id",secret,_=>{},new Stub(_=>Task.FromResult(Reply(n.ToJsonString()))));
            Check(await Reject(async()=>{await auth.BeginAsync(default);}),"untrusted authorization URL rejected");
        }
        {
            using var auth=new DistributedAuthentication("id",secret,_=>{},new Stub(_=>Task.FromResult(Reply(Code()))),()=>instant);
            var code=await auth.BeginAsync(default);instant=instant.AddMinutes(11);
            Check(await Reject(()=>auth.CompleteAsync(code,"x",default)),"expired linking code blocked before HTTP");
        }
        foreach(var mutation in new Action<JsonNode>[] {n=>n.AsObject().Remove("refreshToken"),n=>n["expiresIn"]=0,n=>n["type"]="Basic",n=>n["accessToken"]=new string('x',8001)}){
            var n=JsonNode.Parse(Token())!;mutation(n);
            using var auth=new DistributedAuthentication("id",secret,_=>{},new Stub(req=>Task.FromResult(Reply(req.RequestUri!.AbsolutePath.EndsWith("/userCode")?Code():n.ToJsonString()))));
            var code=await auth.BeginAsync(default);Check(await Reject(()=>auth.CompleteAsync(code,"x",default)),"invalid token contract rejected");
        }
        {
            int count=0;using var auth=new DistributedAuthentication("id",secret,_=>{},new Stub(req=>{count++;return Task.FromResult(Reply(count==1?Code():count==2?Token():"{}",count>2?HttpStatusCode.Unauthorized:HttpStatusCode.OK));}),()=>instant);
            await auth.CompleteAsync(await auth.BeginAsync(default),"x",default);instant=instant.AddMinutes(2);
            Check(await Reject(async()=>{await auth.GetAccessTokenAsync(default);}),"refresh failure invalidates session");
            Check(await Reject(async()=>{await auth.GetAccessTokenAsync(default);}) && count==3,"no refresh retry storm");
        }
        foreach(var body in new[]{"not json",new string('x',262145),"null"}){
            using var auth=new DistributedAuthentication("id",secret,_=>{},new Stub(_=>Task.FromResult(Reply(body))));Check(await Reject(async()=>{await auth.BeginAsync(default);}),"malformed/oversized response sanitized");
        }
        {
            using var auth=new DistributedAuthentication("id",secret,_=>{throw new Exception(secret);},new Stub(req=>Task.FromResult(Reply(req.RequestUri!.AbsolutePath.EndsWith("/userCode")?Code():req.Method==HttpMethod.Post?Token():JsonSerializer.Serialize(new{id=Guid.NewGuid()})))));
            await auth.CompleteAsync(await auth.BeginAsync(default),"x",default);
            Check(await Reject(()=>auth.VerifyMerchantAsync(merchant,default)),"other merchant rejected; logger failures do not break auth");
        }
        Console.WriteLine("AUTH TOTAL "+passed+" PASS; fake HTTP only.");return passed;
    }
}
