using System.Diagnostics;
using System.Text.Json;

namespace LealInfo.Components;

// Mutable transaction journals only. Binary/hash readers deliberately keep their
// stricter sharing. An open reader follows one file handle across atomic renames.
public static class JournalSnapshot
{
    internal const int MaximumBytes=65536;
    internal static FileStream OpenReader(string path)
    {
        path=Path.GetFullPath(path);
        for(var parent=new FileInfo(path).Directory;parent!=null;parent=parent.Parent)
            if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("Redirected journal ancestor");
        var info=new FileInfo(path);
        if(!info.Exists)throw new FileNotFoundException("Transaction journal is not available",path);
        if((info.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)
            throw new InvalidDataException("Redirected transaction journal");
        return new FileStream(path,FileMode.Open,FileAccess.Read,
            FileShare.ReadWrite|FileShare.Delete,8192,FileOptions.SequentialScan);
    }
    static byte[] ReadBounded(FileStream file)
    {
        if(file.Length<=0||file.Length>MaximumBytes)
            throw new InvalidDataException("Invalid transaction journal size");
        using var memory=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=file.Read(buffer,0,buffer.Length))>0)
        {
            if(memory.Length+count>MaximumBytes)throw new InvalidDataException("Transaction journal exceeds bound");
            memory.Write(buffer,0,count);
        }
        return memory.ToArray();
    }
    static JsonDocument ReadOnce(string path)
    {
        byte[] first;
        using(var file=OpenReader(path))
        {
            first=ReadBounded(file);file.Position=0;
            var second=ReadBounded(file);
            if(first.LongLength!=file.Length||!first.AsSpan().SequenceEqual(second))
                throw new IOException("Transaction journal changed while reading");
        } // Close the handle BEFORE returning a parsed snapshot, including errors.
        return JsonDocument.Parse(first);
    }
    public static JsonDocument Read(string path,TimeSpan? timeout=null,CancellationToken cancellation=default)
    {
        var limit=timeout??TimeSpan.FromMilliseconds(500);
        if(limit<=TimeSpan.Zero||limit>TimeSpan.FromSeconds(5))throw new ArgumentOutOfRangeException(nameof(timeout));
        var clock=Stopwatch.StartNew();Exception? last=null;
        for(int attempt=0;attempt<64;attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            try{return ReadOnce(path);}
            catch(InvalidDataException){throw;}
            catch(IOException error){last=error;}
            catch(JsonException error){last=error;}
            if(clock.Elapsed>=limit)break;
            int delay=(int)Math.Max(1,Math.Min(10,(limit-clock.Elapsed).TotalMilliseconds));
            if(cancellation.CanBeCanceled)cancellation.WaitHandle.WaitOne(delay);
            else Thread.Sleep(delay);
        }
        cancellation.ThrowIfCancellationRequested();
        throw new TimeoutException("A coherent transaction journal could not be read within the bounded deadline",last);
    }
}
