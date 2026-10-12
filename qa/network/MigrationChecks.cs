using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LealInfoPDV.Licensing;
using LealInfoPDV.Network;
internal static class MigrationChecks
{
    internal static void Run(string folder)
    {
        using var key=RSA.Create(2048);var pub=key.ExportSubjectPublicKeyInfoPem();
        const string hw="LH-1111-2222-3333-4444", other="LH-AAAA-BBBB-CCCC-DDDD", legacy="LI-OLD-WINDOWS";
        string Sign<T>(T value) {var bytes=JsonSerializer.SerializeToUtf8Bytes(value);return JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(bytes),Convert.ToBase64String(key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));}
        void Assert(bool ok,string reason){if(!ok)throw new Exception(reason);}
        void Reject(Action action,string reason){try{action();}catch(Exception e)when(e is InvalidDataException or InvalidOperationException){Console.WriteLine("PASS: "+reason);return;}throw new Exception("Nao recusou: "+reason);}
        var terms=new LicenseTerms(legacy,2,"unico",null,"QA",17,"ORIGINAL-UNICO","standard","003",true,0);
        var signed=Sign(terms);var path=Path.Combine(folder,"migration-original");
        ProtectedFile.Write(path,new LicenseState{SignedLicense=signed,Devices=new(){new("TERMINAL","PUBLIC-KEY","QA")},LastSeenUtc=DateTimeOffset.UtcNow.AddSeconds(-1)});
        var request=LicenseMigration.CreateRequest(signed,path,hw,legacy,pub);
        Assert(request.Snapshot?.Devices.Count==1,"Exportacao perdeu terminal");
        var approved=Sign(request with {Kind=LicenseMigration.AuthorizationKind});
        var migrated=new NetworkLicense(path,hw,pub,legacy);migrated.ApplyMigration(approved);
        Assert(migrated.RegisteredCount==2 && migrated.Terms==terms,"Migracao alterou plano/pontos/id/revisao");
        var reformatted=new NetworkLicense(path,hw,pub,"LI-NEW-WINDOWS");reformatted.Activate(signed);reformatted.CheckAccess();
        Assert(reformatted.RegisteredCount==2,"MachineGuid alterado perdeu pontos");
        Reject(()=>new NetworkLicense(path,other,pub,"LI-NEW-WINDOWS").CheckAccess(),"autorizacao copiada para hardware B recusada");
        var cleanPath=Path.Combine(folder,"migration-clean");
        var clean=new NetworkLicense(cleanPath,hw,pub,"LI-NEW-WINDOWS");clean.ApplyMigration(approved);clean.Activate(signed);
        Assert(clean.Terms==terms && clean.RegisteredCount==2,"Reinstalacao com checkpoint alterou pontos");
        var restored=new NetworkLicense(cleanPath,hw,pub,"LI-NEW-WINDOWS");Assert(restored.RegisteredCount==2,"Reabertura perdeu checkpoint");
        var broken=Path.Combine(folder,"migration-corrupt");File.WriteAllBytes(broken,new byte[]{1,2,3});
        var brokenBytes=File.ReadAllBytes(broken);
        var brokenHash=Convert.ToHexString(SHA256.HashData(brokenBytes));
        var approvedRestore=Sign(request with{Kind=LicenseMigration.AuthorizationKind,SourceProtectedSha256=brokenHash});
        var recovery=new NetworkLicense(broken,hw,pub,legacy);recovery.ApplyMigration(approvedRestore);
        Assert(recovery.RegisteredCount==2 && File.ReadAllBytes(broken+".preserved."+brokenHash).SequenceEqual(brokenBytes),"Recuperacao nao preservou original/terminais");
        var unknown=Path.Combine(folder,"migration-unknown");File.WriteAllBytes(unknown,new byte[]{7,8,9});
        var unknownOriginal=File.ReadAllBytes(unknown);
        var unknownRequest=LicenseMigration.CreateRequest(signed,unknown,hw,legacy,pub);
        Assert(unknownRequest.Snapshot==null && unknownRequest.StateAvailability=="ILEGIVEL_SEM_CHECKPOINT","Inventou estado ilegivel");
        var authorizedLocal=Sign(unknownRequest with{Kind=LicenseMigration.AuthorizationKind});
        var local=new NetworkLicense(unknown,hw,pub,legacy);local.ApplyMigration(authorizedLocal);local.CheckAccess();local.Activate(signed);
        Assert(local.Terms==terms && File.ReadAllBytes(unknown).SequenceEqual(unknownOriginal),"Uso local sobrescreveu original");
        Reject(()=>local.Register("NEW","NEW","novo"),"recuperacao sem checkpoint nao zera/libera terminais");
        var reopenedLocal=new NetworkLicense(unknown,hw,pub,"LI-NEW-WINDOWS");reopenedLocal.CheckAccess();
        Reject(()=>LicenseMigration.CreateRequest(signed,unknown,hw,"LI-OTHER-WINDOWS",pub),"solicitacao nao autoriza legado de outro Windows sem prova anterior");
        var wrong=Sign(request with{Kind=LicenseMigration.AuthorizationKind,ToHardwareSerial=other});
        Reject(()=>LicenseMigration.ValidateAuthorization(wrong,hw,pub),"destino de migracao diferente recusado");
        var tampered=JsonSerializer.Deserialize<SignedLicense>(approved)!;
        Reject(()=>LicenseMigration.ValidateAuthorization(JsonSerializer.Serialize(tampered with {Payload=Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"))}),hw,pub),"autorizacao adulterada recusada");
        var stale=Path.Combine(folder,"migration-stale");File.WriteAllBytes(stale,new byte[]{1,2,4});
        Reject(()=>new NetworkLicense(stale,hw,pub,legacy).ApplyMigration(approvedRestore),"hash do original alterado bloqueia recuperacao");
        var requestFile=Path.Combine(folder,"migration-request.json");File.WriteAllText(requestFile,JsonSerializer.Serialize(request));
        var privateFile=Path.Combine(folder,"migration-qa-private.pem");File.WriteAllText(privateFile,key.ExportPkcs8PrivateKeyPem());
        var authFile=Path.Combine(folder,"migration-owner.lealmigracao");
        var psi=new System.Diagnostics.ProcessStartInfo("pwsh"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{"-NoProfile","-File",Path.GetFullPath("tools/license-migration/Autorizar_Migracao.ps1"),"-Solicitacao",requestFile,"-ChavePrivada",privateFile,"-Saida",authFile,"-ConfirmoMesmoComputador","-ConfirmoRevisaoAtual"})psi.ArgumentList.Add(arg);
        using(var process=System.Diagnostics.Process.Start(psi)!){var stdout=process.StandardOutput.ReadToEnd();var stderr=process.StandardError.ReadToEnd();process.WaitForExit();Assert(process.ExitCode==0,"Emissor migracao falhou: "+stderr);}
        Assert(LicenseMigration.ValidateAuthorization(File.ReadAllText(authFile),hw,pub).LicenseDigest==request.LicenseDigest,"Emissor e cliente divergem");
        Console.WriteLine("PASS: migracao assinada preserva chave original/id/revisao/plano/pontos, checkpoint, original ilegivel, MachineGuid novo simulado, retorno offline e assinatura do emissor PowerShell.");
    }
}
