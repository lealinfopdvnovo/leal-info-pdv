using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LealInfo.Components;

static class Tests
{
    static int passed;
    static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
    static byte[] Zip(params (string name, byte[] bytes)[] files)
    {
        using var bytes=new MemoryStream();
        using(var z=new ZipArchive(bytes,ZipArchiveMode.Create,true))
            foreach(var file in files){using var s=z.CreateEntry(file.name).Open();s.Write(file.bytes);}
        return bytes.ToArray();
    }
    static readonly byte[] Dll=Encoding.UTF8.GetBytes("opaque-fixture-not-an-executable");
    static byte[] Pet(string identity="003",string version="1.2",string? digest=null)=>Zip(
        ("Extensions/PET/Pet.dll",Dll),
        ("Extensions/PET/manifest.json",JsonSerializer.SerializeToUtf8Bytes(new{Identity=identity,Version=version,ApiVersion=1,Assembly="Pet.dll",Sha256=digest??Hash(Dll)})));
    static byte[] Global()=>Zip(("LealInfoPDV.exe",Dll),("LealInfoPDV.dll",Dll),("LealInfoPDV.deps.json",Encoding.UTF8.GetBytes("{}")));
    static Release ReleaseFor(byte[] bytes,ComponentKind kind=ComponentKind.PET,string identity="003")=>
        new(kind,kind==ComponentKind.PET?"1.2":"10.500",kind==ComponentKind.PET?identity:null,Hash(bytes),
            "https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/"+
            (kind==ComponentKind.PET?"client-"+identity+"-pet-v1.2":"v10.500")+"/package.zip");
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static async Task Case(string name,Func<Task> action){await action();passed++;Console.WriteLine("PASS "+name);}
    static async Task Reject(Func<Task> action)
    {
        try{await action();}catch(InvalidDataException){return;}
        throw new Exception("Unsafe candidate accepted");
    }
    static Task<Staged> Stage(byte[] bytes,string folder,Release? r=null)=>IndependentUpdater.StageAsync(r??ReleaseFor(bytes),"003",folder,(_,_)=>Task.FromResult(bytes));
    static Task Done(Action a){a();return Task.CompletedTask;}
    static async Task Main()
    {
        var root=Path.Combine(Path.GetTempPath(),"LEAL-component-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var baseDir=Path.Combine(root,"installation");Directory.CreateDirectory(baseDir);
        var protectedFile=Path.Combine(baseDir,"LealInfoPDV.dll");File.WriteAllText(protectedFile,"base-must-not-change");
        var sentinel=Hash(File.ReadAllBytes(protectedFile));
        var staging=Path.Combine(root,"staging");
        try
        {
            await Case("independent GLOBAL and PET versions",()=>Done(()=>{
                var p=IndependentUpdater.Plan(new("10.466","1.1","003"),ReleaseFor(Global(),ComponentKind.GLOBAL),ReleaseFor(Pet()));
                Check(p.Available.Count==2&&p.Errors.Count==0,"Independent components missing");
            }));
            await Case("same GLOBAL does not suppress newer PET",()=>Done(()=>{
                var g=ReleaseFor(Global(),ComponentKind.GLOBAL) with{Version="10.466"};
                var p=IndependentUpdater.Plan(new("10.466","1.1","003"),g,ReleaseFor(Pet()));
                Check(p.Available.Count==1&&p.Available[0].Component==ComponentKind.PET,"PET not offered");
            }));
            await Case("same PET does not suppress newer GLOBAL",()=>Done(()=>{
                var p=IndependentUpdater.Plan(new("10.466","1.2","003"),ReleaseFor(Global(),ComponentKind.GLOBAL),ReleaseFor(Pet()));
                Check(p.Available.Count==1&&p.Available[0].Component==ComponentKind.GLOBAL,"GLOBAL not offered");
            }));
            await Case("wrong PET identity does not block GLOBAL",()=>Done(()=>{
                var p=IndependentUpdater.Plan(new("10.466","1.1","003"),ReleaseFor(Global(),ComponentKind.GLOBAL),ReleaseFor(Pet(),ComponentKind.PET,"001"));
                Check(p.Available.Count==1&&p.Errors.Count==1,"Invalid PET blocked valid GLOBAL");
            }));
            await Case("wrong GLOBAL does not block PET",()=>Done(()=>{
                var g=ReleaseFor(Global(),ComponentKind.GLOBAL) with{Identity="003"};
                var p=IndependentUpdater.Plan(new("10.466","1.1","003"),g,ReleaseFor(Pet()));
                Check(p.Available.Count==1&&p.Errors.Count==1,"Invalid GLOBAL blocked valid PET");
            }));
            await Case("valid PET staged outside installation",async()=>{
                var p=await Stage(Pet(),staging);Check(p.Hashes.Count==2,"Missing staged entries");
                Check(Hash(File.ReadAllBytes(protectedFile))==sentinel,"Base changed");
            });
            await Case("valid GLOBAL staged independently",async()=>{
                var b=Global();var p=await Stage(b,staging,ReleaseFor(b,ComponentKind.GLOBAL));Check(p.Hashes.Count==3,"Missing GLOBAL entries");
            });
            await Case("wrong ZIP hash rejected before writes",()=>Reject(()=>Stage(Pet(),staging,ReleaseFor(Pet()) with{Sha256=new string('0',64)})));
            await Case("PET replacing base rejected",()=>Reject(()=>Stage(Zip(("LealInfoPDV.dll",Dll)),staging)));
            await Case("GLOBAL replacing PET rejected",async()=>{
                var b=Pet();await Reject(()=>Stage(b,staging,ReleaseFor(b,ComponentKind.GLOBAL)));
            });
            await Case("ZIP traversal rejected",()=>Reject(()=>Stage(Zip(("../LealInfoPDV.dll",Dll)),staging)));
            await Case("ZIP backslash traversal rejected",()=>Reject(()=>Stage(Zip(("Extensions/PET/..\\LealInfoPDV.dll",Dll)),staging)));
            await Case("case-insensitive duplicate rejected",()=>Reject(()=>Stage(Zip(("Extensions/PET/A.dll",Dll),("Extensions/PET/a.dll",Dll)),staging)));
            await Case("missing manifest rejected",()=>Reject(()=>Stage(Zip(("Extensions/PET/Pet.dll",Dll)),staging)));
            await Case("inner identity rejected",()=>Reject(()=>Stage(Pet("001"),staging)));
            await Case("inner version rejected",()=>Reject(()=>Stage(Pet("003","1.1"),staging)));
            await Case("inner assembly hash rejected",()=>Reject(()=>Stage(Pet(digest:new string('0',64)),staging)));
            await Case("PET script rejected",()=>Reject(()=>Stage(Zip(("Extensions/PET/run.ps1",Dll)),staging)));
            await Case("foreign release URL rejected",()=>Reject(()=>Stage(Pet(),staging,ReleaseFor(Pet()) with{PackageUrl="https://example.invalid/pet.zip"})));
            await Case("invalid PET numbering rejected",()=>Reject(()=>Stage(Pet(),staging,ReleaseFor(Pet()) with{Version="10.500"})));
            await Case("cancellation preserves installation",async()=>{
                using var c=new CancellationTokenSource();c.Cancel();
                try{await IndependentUpdater.StageAsync(ReleaseFor(Pet()),"003",staging,(_,_)=>Task.FromResult(Pet()),c.Token);throw new Exception("Cancellation ignored");}
                catch(OperationCanceledException){}
                Check(Hash(File.ReadAllBytes(protectedFile))==sentinel,"Base changed");
            });
            foreach(var id in new[]{"001","003"})
            foreach(var global in new[]{"10.465","10.470"})
            await Case("compatible PET " + id + " with GLOBAL " + global,()=>Done(()=>{
                var r=ReleaseFor(Pet(),ComponentKind.PET,id) with {MinGlobalVersion="10.465",MaxGlobalVersionExclusive="10.480"};
                var plan=IndependentUpdater.Plan(new(global,"1.1",id),null,r);
                Check(plan.Available.Count==1 && plan.Errors.Count==0,"Compatible PET suppressed");
            }));
            await Case("incompatible PET cannot suppress compatible GLOBAL",()=>Done(()=>{
                var r=ReleaseFor(Pet()) with{MinGlobalVersion="10.470",MaxGlobalVersionExclusive="10.480"};
                var plan=IndependentUpdater.Plan(new("10.465","1.1","003"),ReleaseFor(Global(),ComponentKind.GLOBAL),r);
                Check(plan.Available.Count==1 && plan.Available[0].Component==ComponentKind.GLOBAL && plan.Errors.Count==1,"Compatibility not isolated");
            }));
            await Case("GLOBAL rejected when installed PET excludes target version",()=>Done(()=>{
                var plan=IndependentUpdater.Plan(new("10.465","1.1","003","10.465","10.480"),ReleaseFor(Global(),ComponentKind.GLOBAL),null);
                Check(plan.Available.Count==0 && plan.Errors.Count==1,"Incompatible GLOBAL offered");
            }));
            await Case("compatibility upper bound exclusive",()=>Done(()=>{
                var r=ReleaseFor(Pet()) with{MinGlobalVersion="10.465",MaxGlobalVersionExclusive="10.480"};
                var plan=IndependentUpdater.Plan(new("10.480","1.1","003"),null,r);
                Check(plan.Available.Count==0 && plan.Errors.Count==1,"Exclusive bound ignored");
            }));
            await Case("extra PET resource rejected rather than silently ignored by worker",()=>Reject(()=>Stage(Zip(
                ("Extensions/PET/Pet.dll",Dll),("Extensions/PET/logo.png",Dll)),staging)));
            await Case("feed cannot claim compatibility absent from package",()=>Reject(()=>Stage(Pet(),staging,
                ReleaseFor(Pet()) with{MinGlobalVersion="10.465",MaxGlobalVersionExclusive="10.480"})));
            await Case("unsupported API rejected",()=>Reject(()=>Stage(Pet(),staging,ReleaseFor(Pet()) with{PetApiVersion=2})));
            Check(Hash(File.ReadAllBytes(protectedFile))==sentinel,"Protected base changed after rejection");
            Directory.CreateDirectory("pair-evidence");
            File.WriteAllText("pair-evidence/component-tests.json",JsonSerializer.Serialize(new{Passed=passed,InstallationUntouched=true,ProductionPublished=false,Scope="selection and verified staging; activation/UI/migration pending"}));
            Console.WriteLine("TOTAL "+passed);
        }
        finally{Directory.Delete(root,true);}
    }
}
