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
    private static KitchenPending Read(string path) => JsonSerializer.Deserialize<KitchenPending>(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser))
        ?? throw new InvalidDataException("Pendência inválida; arquivo preservado.");
    internal IReadOnlyList<KitchenPending> Pending() { lock (gate) return Directory.GetFiles(folder, "*.pending").Order().Select(Read).ToArray(); }
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
            return count;
        }
    }
}
