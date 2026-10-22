using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Leal.Diagnostics;
internal sealed class Collector
{
    readonly Stopwatch watch=Stopwatch.StartNew();readonly int? requestedPid;int? previousPid;DateTime? previousStart;double? previousCpu;double previousTime;long oldIdle,oldKernel,oldUser;bool systemInitialized;double? oldOwnCpu;double oldOwnTime;
    internal Collector(int? pid){requestedPid=pid;}
    internal static Machine Computer(){var ram=Native.Ram();using var own=Process.GetCurrentProcess();return new(RuntimeInformation.OSDescription,RuntimeInformation.OSArchitecture.ToString(),Native.Processor(),Environment.ProcessorCount,(long)ram.TotalPhysical,own.WorkingSet64,Drives());}
    static string Drives()
    {
        var list=new List<string>();foreach(var drive in DriveInfo.GetDrives())try{if(drive.DriveType==DriveType.Fixed&&drive.IsReady)list.Add($"{drive.Name} {Units.Size(drive.AvailableFreeSpace)} livres de {Units.Size(drive.TotalSize)}");}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}return string.Join("; ",list);
    }
    internal static Process[] FindPdv()=>Process.GetProcessesByName("LealInfoPDV");
    internal Sample Take(string stage)
    {
        double seconds=watch.Elapsed.TotalSeconds;double? system=null;
        if(Native.GetSystemTimes(out long idle,out long kernel,out long user)){long all=kernel-oldKernel+user-oldUser;if(systemInitialized&&all>0)system=Math.Clamp(100d*(all-(idle-oldIdle))/all,0,100);oldIdle=idle;oldKernel=kernel;oldUser=user;systemInitialized=true;}
        var net=Native.Network();var ram=Native.Ram();int? pid=null;double? cpu=null;long? working=null,priv=null;ulong? read=null,write=null;int? tcp=null;string? note=null;
        Process? process=null;
        try
        {
            if(requestedPid.HasValue)process=Process.GetProcessById(requestedPid.Value);
            else{var found=FindPdv();if(found.Length==1)process=found[0];else{foreach(var p in found)p.Dispose();note=found.Length>1?"Mais de um PDV aberto; selecione o PID antes de analisar.":"PDV não encontrado nesta amostra.";}}
            if(process!=null)
            {
                process.Refresh();if(process.HasExited)throw new InvalidOperationException();pid=process.Id;var start=process.StartTime;double processor=process.TotalProcessorTime.TotalSeconds;
                if(previousPid==pid&&previousStart==start&&previousCpu.HasValue&&seconds>previousTime)cpu=Math.Clamp((processor-previousCpu.Value)/(seconds-previousTime)/Environment.ProcessorCount*100,0,100);
                previousPid=pid;previousStart=start;previousCpu=processor;previousTime=seconds;
                working=process.WorkingSet64;priv=process.PrivateMemorySize64;
                if(Native.GetProcessIoCounters(process.Handle,out var io)){read=io.ReadBytes;write=io.WriteBytes;}
                tcp=Native.Tcp(pid.Value);
            }
            else{previousPid=null;previousStart=null;previousCpu=null;}
        }
        catch(Exception e)when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {note="Processo encerrado ou acesso indisponível. Não foi solicitada elevação.";previousPid=null;previousCpu=null;}
        finally{process?.Dispose();}
        using var analyzer=Process.GetCurrentProcess();double ownTotal=analyzer.TotalProcessorTime.TotalSeconds;double? ownCpu=oldOwnCpu.HasValue&&seconds>oldOwnTime?Math.Clamp((ownTotal-oldOwnCpu.Value)/(seconds-oldOwnTime)/Environment.ProcessorCount*100,0,100):null;oldOwnCpu=ownTotal;oldOwnTime=seconds;
        return new(seconds,stage,pid,cpu,system,working,priv,(long)ram.AvailablePhysical,read,write,tcp,net.received,net.sent,pid.HasValue&&working.HasValue,note,ownCpu,analyzer.WorkingSet64);
    }
}
internal static class FileInventory
{
    internal static Inventory Scan(string? folder,CancellationToken token,int maxEntries=50000,int budgetMs=20000)
    {
        if(string.IsNullOrWhiteSpace(folder)||!Directory.Exists(folder))return new(null,null,0,0,new(),new(),new(),0,false,"Pasta não selecionada ou indisponível.");
        var watch=Stopwatch.StartNew();var stack=new Stack<string>();stack.Push(folder);var files=new List<FileEntry>();int skipped=0,entries=0;bool complete=true;
        while(stack.Count>0)
        {
            if(token.IsCancellationRequested||watch.ElapsedMilliseconds>budgetMs||entries>=maxEntries){complete=false;break;}
            string dir=stack.Pop();
            try
            {
                if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0){skipped++;continue;}
                foreach(var path in Directory.EnumerateFileSystemEntries(dir))
                {
                    if(token.IsCancellationRequested||watch.ElapsedMilliseconds>budgetMs||++entries>maxEntries){complete=false;break;}
                    try
                    {
                        var attributes=File.GetAttributes(path);if((attributes&FileAttributes.ReparsePoint)!=0){skipped++;continue;}
                        if((attributes&FileAttributes.Directory)!=0)stack.Push(path);
                        else{var f=new FileInfo(path);files.Add(new(Path.GetRelativePath(folder,path),f.Length,f.Extension.ToLowerInvariant()));}
                    }
                    catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){skipped++;}
                }
            }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){skipped++;}
            if(!complete)break;
        }
        string? version=null;try{var exe=Path.Combine(folder,"LealInfoPDV.exe");if(File.Exists(exe))version=FileVersionInfo.GetVersionInfo(exe).FileVersion;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){}
        var groups=files.GroupBy(f=>f.Extension).Select(g=>new FileGroup(g.Key,g.Count(),g.Sum(f=>f.Bytes))).OrderByDescending(g=>g.Bytes).ToList();
        var imageExtensions=new HashSet<string>{".png",".jpg",".jpeg",".webp",".bmp",".ico"};
        var duplicates=files.Where(f=>f.Bytes>0&&imageExtensions.Contains(f.Extension)).GroupBy(f=>(f.Bytes,f.Extension)).Where(g=>g.Count()>1).OrderByDescending(g=>g.Key.Bytes*g.Count()).Take(20).Select(g=>new DuplicateCandidate(g.Key.Bytes,g.Key.Extension,g.Take(10).Select(f=>f.RelativeName).ToArray())).ToList();
        return new(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)),version,files.Count,files.Sum(f=>f.Bytes),groups,files.OrderByDescending(f=>f.Bytes).Take(25).ToList(),duplicates,skipped,complete,complete?"Inventário de metadados concluído. Não foram abertos bancos, licenças ou configurações.":"Inventário parcial: limite de tempo/entradas ou interrupção. Tamanhos são apenas dos arquivos observados.");
    }
}
