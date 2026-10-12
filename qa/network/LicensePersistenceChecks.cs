using System.Security.Cryptography;
using System.Text;
using LealInfoPDV.Licensing;
using LealInfoPDV.Network;
internal static class LicensePersistenceChecks
{
    internal static void Run(string folder, string publicKey, Func<LicenseTerms,string> sign)
    {
        void Assert(bool value,string message) { if(!value)throw new Exception(message); }
        void Reject(Action action,string scenario) { try { action(); } catch(Exception ex) when(ex is InvalidDataException or InvalidOperationException) { Console.WriteLine("PASS: "+scenario);return; } throw new Exception("Não bloqueou: "+scenario); }
        byte[] Smbios(byte first)
        {
            var raw=new byte[35];BitConverter.GetBytes((uint)27).CopyTo(raw,4);
            raw[8]=1;raw[9]=25;
            for(var i=0;i<16;i++)raw[16+i]=(byte)(first+i);
            return raw;
        }
        var serialA=MachineLicenseIdentity.FromSmbios(Smbios(1))!;
        var serialB=MachineLicenseIdentity.FromSmbios(Smbios(30))!;
        Assert(serialA!=serialB,"UUIDs diferentes produziram serial igual");
        Assert(serialA==MachineLicenseIdentity.FromSmbios(Smbios(1)),"Identidade mudou com mesma tabela firmware");
        var bad=Smbios(1);Array.Clear(bad,16,16);Assert(MachineLicenseIdentity.FromSmbios(bad)==null,"UUID nulo aceito");
        Assert(MachineLicenseIdentity.FromSmbios(new byte[3])==null,"Tabela truncada aceita");
        Console.WriteLine("PASS: identidade hardware determinística, UUID inválido/truncado recusado; formatação apenas simulada.");
        var terms=new LicenseTerms(serialA,2,"unico",null,"QA",10,"PERMANENTE","standard","003",true,0);
        var signed=sign(terms);var text="LEAL1-"+Convert.ToBase64String(Encoding.UTF8.GetBytes(signed));
        Assert(LicenseInput.Normalize(text)==signed && LicenseInput.Normalize("\uFEFF"+signed)==signed,"Arquivo/texto divergem");
        var path=Path.Combine(folder,"permanent-persistence");var store=new NetworkLicense(path,serialA,publicKey);
        store.Activate(signed);store.Activate(text);store.Import(signed);
        store.Register("TERMINAL","PUBLIC","QA");
        var reopened=new NetworkLicense(path,serialA,publicKey);reopened.CheckAccess();
        Assert(reopened.Terms==terms && reopened.RegisteredCount==2,"Reabertura perdeu licença/terminal");
        reopened.Activate(text);Assert(reopened.RegisteredCount==2,"Reativação duplicou pontos");
        Reject(()=>new NetworkLicense(path,serialB,publicKey).CheckAccess(),"licença A no hardware B recusada");
        var cleanPath=Path.Combine(folder,"reinstalled-clean");
        var clean=new NetworkLicense(cleanPath,serialA,publicKey);clean.Activate(text);Assert(clean.Terms==terms,"Reinstalação limpa não aceitou mesma chave");
        Console.WriteLine("PASS: arquivo/texto equivalentes, reimportação idempotente, reabertura e reinstalação limpa simulada com UUID preservado.");
        var vault=File.ReadAllText(path+".signed");Assert(vault==signed,"Cópia assinada foi reserializada/adulterada");
        var protectedBytes=File.ReadAllBytes(path);
        File.WriteAllBytes(path,new byte[]{1,2,3});
        var brokenBytes=File.ReadAllBytes(path);
        var recovered=new NetworkLicense(path,serialA,publicKey);
        Reject(recovered.CheckAccess,"copia assinada sozinha nao libera estado ilegivel sem autorizacao");
        Assert(recovered.Terms==terms && recovered.NeedsStateRecovery,"Licença independente não recuperou validação permanente");
        Reject(()=>recovered.Register("NEW","NEW","novo"),"estado ilegível não libera novo terminal");
        Assert(File.ReadAllBytes(path).SequenceEqual(brokenBytes),"Estado ilegível foi substituído");
        Console.WriteLine("PASS: assinatura independente permanece verificavel, mas falha DPAPI exige autorizacao vinculada ao original; cadastro preservado.");
        File.WriteAllBytes(path,protectedBytes);
        var healthy=new NetworkLicense(path,serialA,publicKey);Assert(healthy.RegisteredCount==2,"Restauração perdeu cadastro");
        healthy.Import(sign(terms with {Active=false,Revision=11}));
        Reject(healthy.CheckAccess,"licença inativa recusada");
        Reject(()=>healthy.Activate(signed),"revisão supersedida não reativa");
        var monthlyPath=Path.Combine(folder,"monthly-protected");var monthly=new NetworkLicense(monthlyPath,serialA,publicKey);
        monthly.Activate(sign(terms with {LicenseId="MONTHLY",BillingMode="mensal",ExpiresUtc=DateTimeOffset.UtcNow.AddDays(10)}));
        File.WriteAllBytes(monthlyPath,new byte[]{1,2,3});
        Reject(()=>new NetworkLicense(monthlyPath,serialA,publicKey).CheckAccess(),"mensalidade não perde proteção de relógio quando estado é ilegível");
        var tamperPath=Path.Combine(folder,"tampered-signed");var tamper=new NetworkLicense(tamperPath,serialA,publicKey);tamper.Activate(signed);
        File.WriteAllText(tamperPath+".signed",signed.Replace("Payload","Adulterado"));
        bool tamperBlocked=false;try { _=new NetworkLicense(tamperPath,serialA,publicKey).Terms; } catch { tamperBlocked=true; }
        Assert(tamperBlocked,"Cópia assinada adulterada aceita");
        var interrupted=Path.Combine(folder,"interrupted-save");
        var beforeInterrupt=new NetworkLicense(interrupted,serialA,publicKey);beforeInterrupt.Activate(signed);
        var committedState=ProtectedFile.Read<LicenseState>(interrupted);
        committedState.SignedLicense=sign(terms with {Revision=12,Active=false});
        ProtectedFile.Write(interrupted,committedState); // simula queda antes de atualizar a cópia assinada
        var afterInterrupt=new NetworkLicense(interrupted,serialA,publicKey);
        Assert(afterInterrupt.Terms.Revision==12 && !afterInterrupt.Terms.Active,"Interrupção escolheu revisão antiga");
        Reject(afterInterrupt.CheckAccess,"interrupção não reativa licença inativa");
        Assert(File.ReadAllText(interrupted+".signed")==committedState.SignedLicense,"Interrupção não reconciliou gravação autorizada");
        var legacyPath=Path.Combine(folder,"legacy-migration");
        var legacyTerms=terms with {ServerSerial="LEGACY-SAME-WINDOWS"};
        ProtectedFile.Write(legacyPath,new LicenseState{SignedLicense=sign(legacyTerms)});
        var migrated=new NetworkLicense(legacyPath,serialA,publicKey,legacyTerms.ServerSerial);migrated.CheckAccess();
        Assert(migrated.Terms==legacyTerms && File.Exists(legacyPath+".signed"),"Migração legada alterou termos");
        Reject(()=>new NetworkLicense(legacyPath,serialA,publicKey,"LEGACY-NEW-WINDOWS").CheckAccess(),"legado não finge sobreviver a MachineGuid alterado");
        var oldBroken=Path.Combine(folder,"old-broken");File.WriteAllBytes(oldBroken,new byte[]{1,2,3});
        Reject(()=>new NetworkLicense(oldBroken,serialA,publicKey).Activate(signed),"legado ilegível sem cópia não zera cadastro nem revisões");
        Console.WriteLine("PASS: compatibilidade legada no mesmo Windows, migração assinada, adulteração recusada e legado sem cópia preservado.");
    }
}
