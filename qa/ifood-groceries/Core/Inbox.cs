using System.Text.Json;
namespace LealInfo.IFood.Groceries;

public sealed record Signal(Guid EventId, Guid OrderId, Guid MerchantId, string Code);
public sealed record Receipt(bool ReadyToAcknowledge, bool Duplicate, Order? Order);
public sealed class Inbox : IDisposable
{
    public sealed record StoredEvent(Guid OrderId, Guid MerchantId, string Code);
    public sealed class State
    {
        public Dictionary<Guid, StoredEvent> Events { get; set; } = new();
        public Dictionary<Guid, Order> Orders { get; set; } = new();
    }
    private readonly string path;
    private readonly Guid merchant;
    private readonly FileStream lease;
    private readonly SemaphoreSlim serial = new(1);
    private State state;
    public Inbox(string isolatedDirectory, Guid testMerchant)
    {
        if (testMerchant == Guid.Empty) throw new ArgumentException("Test merchant required");
        merchant = testMerchant; Directory.CreateDirectory(isolatedDirectory);
        path = Path.Combine(isolatedDirectory, "ifood-test-inbox.json");
        lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > 32_000_000) throw new InvalidDataException("Inbox size exceeded");
            state = File.Exists(path) ? JsonSerializer.Deserialize<State>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid inbox") : new State();
            if (state.Orders.Values.Any(o => o.MerchantId != merchant) || state.Events.Values.Any(e => e.MerchantId != merchant)) throw new InvalidDataException("Inbox merchant mismatch");
        }
        catch { lease.Dispose(); throw; }
    }
    public async Task<Receipt> ReceiveAsync(Signal s, Func<Guid, CancellationToken, Task<string>> fetch, CancellationToken ct = default)
    {
        if (s.EventId == Guid.Empty || s.OrderId == Guid.Empty || s.MerchantId != merchant) throw new ContractException("event identity/merchant");
        if (s.Code != "READY_FOR_INVOICE" && s.Code != "RFI") return new Receipt(false, false, null);
        await serial.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (state.Events.TryGetValue(s.EventId, out var existing))
            {
                if (existing.OrderId != s.OrderId || existing.MerchantId != s.MerchantId) throw new ContractException("event id conflict");
                return new Receipt(true, true, state.Orders[s.OrderId]);
            }
            if (state.Events.Count >= 10_000) throw new InvalidOperationException("Test inbox full; maintenance required");
            var order = VirtualBag.Parse(await fetch(s.OrderId, ct).ConfigureAwait(false), s.OrderId, merchant);
            var next = new State { Events = new(state.Events), Orders = new(state.Orders) };
            next.Events.Add(s.EventId, new StoredEvent(s.OrderId, s.MerchantId, "RFI")); next.Orders[s.OrderId] = order;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(next);
            if (bytes.Length > 32_000_000) throw new InvalidOperationException("Test inbox full");
            var temp = path + ".tmp";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
            // One atomic snapshot contains event and order. A crash before ACK can safely replay.
            File.Move(temp, path, true); state = next;
            return new Receipt(true, false, order);
        }
        finally { serial.Release(); }
    }
    public void Dispose() { lease.Dispose(); serial.Dispose(); }
}
