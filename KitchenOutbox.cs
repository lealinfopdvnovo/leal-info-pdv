using System.Security.Cryptography;
using System.Text.Json;

namespace LealInfoPDV;

internal sealed record KitchenPending(string StoreId, KitchenDraft Draft);
// A durable local outbox: acknowledge only after a real server commit. Retrying the same UUID is safe.
internal sealed class KitchenOutbox
{
    private readonly string folder;
    private readonly object gate = new();
    internal KitchenOutbox(string folder) { this.folder = folder; Directory.CreateDirectory(folder); }
    internal void Queue(string storeId, KitchenDraft draft)
    {
        draft.Validate(); if (!Guid.TryParse(storeId, out var parsed) || parsed == Guid.Empty) throw new InvalidDataException("Base da cozinha não identificada.");
        lock (gate)
        {
            string path = Path.Combine(folder, draft.Id.ToString("D") + ".pending");
            if (File.Exists(path))
            {
                var existing = Read(path);
                if (existing.StoreId != storeId || existing.Draft.Hash != draft.Hash) throw new InvalidOperationException("Pedido já está na fila com dados diferentes.");
                return;
            }
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(new KitchenPending(storeId, draft)), null, DataProtectionScope.CurrentUser);
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
                File.Move(temp, path, false);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    internal void QueueBatch(string storeId,IReadOnlyList<KitchenDraft> drafts)
    {
        if(!Guid.TryParse(storeId,out var identity)||identity==Guid.Empty||drafts.Count is <1 or >2||drafts.Select(d=>d.Id).Distinct().Count()!=drafts.Count||drafts.Select(d=>d.Reference).Distinct().Count()!=1)throw new InvalidDataException("Pedido misto inválido.");
        foreach(var draft in drafts)draft.Validate();
        lock(gate)
        {
            var entries=drafts.Select(d=>new KitchenPending(storeId,d)).ToArray();
            string path=Path.Combine(folder,drafts[0].Id.ToString("D")+".batch");
            if(File.Exists(path)){var saved=ReadBatch(path);if(!saved.Select(p=>(p.StoreId,p.Draft.Hash)).SequenceEqual(entries.Select(p=>(p.StoreId,p.Draft.Hash))))throw new InvalidOperationException("Pedido misto já guardado com outros dados.");return;}
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{var bytes=ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(entries),null,DataProtectionScope.CurrentUser);using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes);file.Flush(true);}File.Move(temp,path,false);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    private static KitchenPending[] ReadBatch(string path)
    {
        var entries=JsonSerializer.Deserialize<KitchenPending[]>(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser))??throw new InvalidDataException("Pedido misto inválido; arquivo preservado.");
        if(entries.Length is <1 or >2||entries.Select(p=>p.StoreId).Distinct().Count()!=1||entries.Select(p=>p.Draft.Id).Distinct().Count()!=entries.Length)throw new InvalidDataException("Pedido misto inválido; arquivo preservado.");
        foreach(var entry in entries)entry.Draft.Validate();return entries;
    }
    private static KitchenPending Read(string path) => JsonSerializer.Deserialize<KitchenPending>(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser))
        ?? throw new InvalidDataException("Pendência inválida; arquivo preservado.");
    internal IReadOnlyList<KitchenPending> Pending() { lock (gate) return Directory.GetFiles(folder, "*.pending").Order().Select(Read).Concat(Directory.GetFiles(folder,"*.batch").Order().SelectMany(ReadBatch)).ToArray(); }
    internal int Flush(KitchenStore store)
    {
        lock (gate)
        {
            int count = 0; string actual = store.StoreId();
            foreach (string path in Directory.GetFiles(folder, "*.pending").Order())
            {
                var entry = Read(path);
                if (entry.StoreId != actual) throw new InvalidOperationException("Pendência de outra base recusada; arquivos preservados.");
                store.Submit(entry.StoreId, entry.Draft); File.Delete(path); count++;
            }
            foreach(string path in Directory.GetFiles(folder,"*.batch").Order())
            {
                var entries=ReadBatch(path);
                if(entries.Any(e=>e.StoreId!=actual))throw new InvalidOperationException("Pedido misto de outra base recusado; arquivo preservado.");
                foreach(var entry in entries){store.Submit(entry.StoreId,entry.Draft);count++;}
                File.Delete(path);
            }
            return count;
        }
    }
}
