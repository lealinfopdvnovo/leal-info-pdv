using System.Diagnostics;
namespace LicAi;
internal static class LiaLicenseGate
{
    internal static bool Allowed()
    {
        try {
            var pdv=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","LealInfoPDV.exe"));
            using var process=Process.Start(new ProcessStartInfo(pdv,"--check-lia") {UseShellExecute=false,CreateNoWindow=true});
            if(process==null)return false;
            if(!process.WaitForExit(15000)){process.Kill();return false;}
            return process.ExitCode==0;
        }catch{return false;}
    }
}
