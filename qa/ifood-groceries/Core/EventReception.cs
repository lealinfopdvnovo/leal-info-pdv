#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace LealInfo.IFood.Groceries
{
    // Shared C# 5 source: the Windows receiver executes exactly this tested implementation.
    // Stage 2 is an event inbox, not order confirmation, picking or financial import.
    public sealed class ReceptionFailure : Exception
    {
        public bool Retryable { get; private set; }
        public int RetrySeconds { get; private set; }
        public ReceptionFailure(string message, bool retryable = false, int retrySeconds = 0) : base(message)
        { Retryable = retryable; RetrySeconds = retrySeconds; }
    }

    [DataContract]
    public sealed class ReceivedEvent
    {
        [DataMember(IsRequired = true)] public string Id { get; internal set; }
        [DataMember(IsRequired = true)] public string OrderId { get; internal set; }
        [DataMember(IsRequired = true)] public string MerchantId { get; internal set; }
        [DataMember(IsRequired = true)] public string Code { get; internal set; }
        [DataMember(IsRequired = true)] public string FullCode { get; internal set; }
        [DataMember(IsRequired = true)] public string CreatedAt { get; internal set; }
        [DataMember(IsRequired = true)] public bool AckAccepted { get; internal set; }
        // R6: true only after normalized final bag is durably stored. No PDV/stock/finance posting.
        // Field name retained for compatibility with the R5 event journal. ACK is transport-only.
        [DataMember(IsRequired = true)] public bool BusinessProcessed { get; internal set; }
        public bool AwaitingFinalBag { get { return Code == "RFI" || FullCode == "READY_FOR_INVOICE"; } }
        internal ReceivedEvent Copy() { return (ReceivedEvent)MemberwiseClone(); }
    }

    internal static class ReceptionJson
    {
        internal static T Read<T>(byte[] bytes)
        {
            try
            {
                ValidateBoundary(bytes);
                using (var input = new MemoryStream(bytes))
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(input, new XmlDictionaryReaderQuotas {
                    MaxDepth = 32, MaxStringContentLength = 2000000, MaxArrayLength = 250000, MaxBytesPerRead = 4096, MaxNameTableCharCount = 16384 }))
                {
                    reader.MoveToContent();
                    if (reader.GetAttribute("type") != (typeof(T).IsArray ? "array" : "object"))
                        throw new SerializationException();
                    var value = (T)new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 250000 }).ReadObject(reader);
                    if (reader.MoveToContent() != XmlNodeType.None) throw new SerializationException();
                    return value;
                }
            }
            catch { throw new ReceptionFailure("Formato de dados invalido. Recepcao interrompida sem confirmar o lote."); }
        }
        // Framework's JSON reader can ignore a second root value; enforce one bounded document.
        internal static void ValidateDocument(byte[] bytes) { ValidateBoundary(bytes); }
        private static void ValidateBoundary(byte[] bytes)
        {
            string json = new UTF8Encoding(false, true).GetString(bytes);
            var stack = new Stack<char>(); bool quoted = false, escaped = false, started = false, ended = false;
            foreach (char c in json)
            {
                if (ended) { if (c != ' ' && c != '\r' && c != '\n' && c != '\t') throw new SerializationException(); continue; }
                if (quoted)
                { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
                if (!started)
                {
                    if (c == ' ' || c == '\r' || c == '\n' || c == '\t') continue;
                    if (c != '[' && c != '{') throw new SerializationException();
                    started = true;
                }
                if (c == '"') quoted = true;
                else if (c == '[' || c == '{') { stack.Push(c); if (stack.Count > 32) throw new SerializationException(); }
                else if (c == ']' || c == '}')
                {
                    if (stack.Count == 0 || stack.Pop() != (c == ']' ? '[' : '{')) throw new SerializationException();
                    if (stack.Count == 0) ended = true;
                }
            }
            if (!ended || quoted || stack.Count != 0) throw new SerializationException();
        }
        internal static byte[] Write<T>(T value)
        {
            using (var output = new MemoryStream())
            { new DataContractJsonSerializer(typeof(T)).WriteObject(output, value); return output.ToArray(); }
        }
        internal static DateTimeOffset Timestamp(string value)
        {
            DateTimeOffset stamp;
            if (value == null || !Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$") ||
                !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out stamp))
                throw new ReceptionFailure("Horario de evento invalido.");
            return stamp;
        }
        internal static void Validate(ReceivedEvent item, Guid merchant)
        {
            Guid id, order, shop;
            if (item == null || !Guid.TryParse(item.Id, out id) || id == Guid.Empty ||
                !Guid.TryParse(item.OrderId, out order) || order == Guid.Empty ||
                !Guid.TryParse(item.MerchantId, out shop) || shop != merchant ||
                item.Code == null || !Regex.IsMatch(item.Code, "^[A-Z0-9_]{1,80}$") ||
                item.FullCode == null || !Regex.IsMatch(item.FullCode, "^[A-Z0-9_]{1,100}$"))
                throw new ReceptionFailure("Identidade, loja ou codigo de evento invalido. Lote nao confirmado.");
            Timestamp(item.CreatedAt);
        }
    }

    public sealed class EventJournal : IDisposable
    {
        private readonly object stateGate=new object();
        private const int MaximumEvents = 10000;
        private const int MaximumBytes = 16000000;
        private readonly string path;
        private readonly Guid merchant;
        private readonly FileStream lease;
        private JournalState state;
        [DataContract] private sealed class JournalState
        {
            [DataMember(IsRequired = true)] public int Version;
            [DataMember(IsRequired = true)] public string Merchant;
            [DataMember(IsRequired = true)] public string NextAttemptUtc;
            [DataMember(IsRequired = true)] public List<ReceivedEvent> Events;
        }
        public EventJournal(string directory, Guid testMerchant)
        {
            if (testMerchant == Guid.Empty) throw new ReceptionFailure("Loja de testes obrigatoria.");
            merchant = testMerchant; Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "events-v1.json");
            lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length > MaximumBytes) throw new ReceptionFailure("Fila local excedeu o limite.");
                    state = ReceptionJson.Read<JournalState>(File.ReadAllBytes(path));
                    if (state == null || state.Version != 1 || state.Merchant != merchant.ToString("D") || state.Events == null || state.Events.Count > MaximumEvents)
                        throw new ReceptionFailure("Fila local incompativel. Preserve a pasta para analise.");
                    ReceptionJson.Timestamp(state.NextAttemptUtc);
                    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var row in state.Events)
                    { ReceptionJson.Validate(row, merchant); if (!ids.Add(row.Id)) throw new ReceptionFailure("Fila local com IDs duplicados."); }
                }
                else state = new JournalState { Version = 1, Merchant = merchant.ToString("D"), NextAttemptUtc = DateTimeOffset.MinValue.ToString("O"), Events = new List<ReceivedEvent>() };
            }
            catch { lease.Dispose(); throw; }
        }
        public DateTimeOffset NextAttemptUtc { get { lock(stateGate) { return ReceptionJson.Timestamp(state.NextAttemptUtc); } } }
        public IList<ReceivedEvent> Snapshot() { lock(stateGate) { return state.Events.Select(e => e.Copy()).ToList().AsReadOnly(); } }
        public IList<string> PendingIds() { lock(stateGate) { return state.Events.Where(e => !e.AckAccepted).Select(e => e.Id).ToList().AsReadOnly(); } }
        private JournalState Clone()
        { return new JournalState { Version = 1, Merchant = state.Merchant, NextAttemptUtc = state.NextAttemptUtc, Events = state.Events.Select(e => e.Copy()).ToList() }; }
        public void Schedule(DateTimeOffset next)
        { lock(stateGate) { var draft = Clone(); draft.NextAttemptUtc = next.ToUniversalTime().ToString("O"); Commit(draft); } }
        public int Stage(IList<ReceivedEvent> batch)
        { lock(stateGate) {
            var draft = Clone(); var index = draft.Events.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase); int added = 0;
            foreach (var item in batch)
            {
                ReceptionJson.Validate(item, merchant);
                ReceivedEvent previous;
                if (index.TryGetValue(item.Id, out previous))
                {
                    if (!string.Equals(previous.OrderId, item.OrderId, StringComparison.OrdinalIgnoreCase) || previous.Code != item.Code ||
                        previous.FullCode != item.FullCode || ReceptionJson.Timestamp(previous.CreatedAt) != ReceptionJson.Timestamp(item.CreatedAt))
                        throw new ReceptionFailure("Conflito de identidade de evento. Lote nao confirmado.");
                    previous.AckAccepted = false; // A redelivery must be acknowledged again, without duplicate processing.
                }
                else
                {
                    if (draft.Events.Count >= MaximumEvents) throw new ReceptionFailure("Fila de testes cheia. Preserve os dados antes de continuar.");
                    var row = item.Copy(); row.AckAccepted = false; row.BusinessProcessed = false;
                    draft.Events.Add(row); index.Add(row.Id, row); added++;
                }
            }
            draft.Events = draft.Events.OrderBy(e => ReceptionJson.Timestamp(e.CreatedAt)).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
            Commit(draft); return added;
        } }
        public void MarkAckAccepted(IList<string> ids)
        { lock(stateGate) {
            var selected = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase); var draft = Clone();
            if (selected.Count != ids.Count || draft.Events.Count(e => selected.Contains(e.Id)) != selected.Count)
                throw new ReceptionFailure("Confirmacao local sem evento persistido.");
            foreach (var row in draft.Events) if (selected.Contains(row.Id)) row.AckAccepted = true;
            Commit(draft);
        } }
        public void MarkBagPersisted(string id)
        { lock(stateGate) {
            var draft=Clone();var row=draft.Events.SingleOrDefault(e=>string.Equals(e.Id,id,StringComparison.OrdinalIgnoreCase));
            if(row==null || !row.AwaitingFinalBag)throw new ReceptionFailure("Sacola sem evento RFI persistido.");
            row.BusinessProcessed=true;Commit(draft);
        } }
        private void Commit(JournalState draft)
        {
            var bytes = ReceptionJson.Write(draft);
            if (bytes.Length > MaximumBytes) throw new ReceptionFailure("Fila de testes excedeu o limite.");
            string temporary = path + ".tmp";
            using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            state = draft; // Publish only after atomic persistence succeeds.
        }
        public void Dispose() { lease.Dispose(); }
    }

    public sealed class EventsClient : IDisposable
    {
        public const string PollRoute = "/events/v1.0/events:polling";
        public const string AckRoute = "/events/v1.0/events/acknowledgment";
        private readonly HttpClient http;
        private readonly Func<CancellationToken, Task<string>> token;
        private readonly Action<AuthenticationEvidence> evidence;
        private readonly Guid merchant;
        public EventsClient(Guid testMerchant, Func<CancellationToken, Task<string>> tokenProvider,
            Action<AuthenticationEvidence> log, HttpMessageHandler testHandler = null)
        {
            if (testMerchant == Guid.Empty || tokenProvider == null) throw new ArgumentException("Test merchant and token provider required");
            merchant = testMerchant; token = tokenProvider; evidence = log;
            http = new HttpClient(testHandler ?? new HttpClientHandler { AllowAutoRedirect = false });
            http.Timeout = TimeSpan.FromSeconds(25);
        }
        public async Task<IList<ReceivedEvent>> PollAsync(CancellationToken ct)
        {
            var body = await SendAsync(HttpMethod.Get, PollRoute, null, ct).ConfigureAwait(false);
            if (body.Length == 0) return new List<ReceivedEvent>();
            var raw = ReceptionJson.Read<EventDto[]>(body);
            if (raw == null || raw.Length > 2000) throw new ReceptionFailure("Lote inesperado ou acima de 2000 eventos.");
            var rows = new List<ReceivedEvent>();
            foreach (var e in raw)
            {
                if (e == null) throw new ReceptionFailure("Evento vazio no lote.");
                var item = new ReceivedEvent { Id = e.id, OrderId = e.orderId, MerchantId = e.merchantId,
                    Code = e.code, FullCode = e.fullCode, CreatedAt = e.createdAt };
                ReceptionJson.Validate(item, merchant); rows.Add(item);
            }
            return rows.AsReadOnly();
        }
        public async Task AcknowledgeAsync(IList<string> ids, CancellationToken ct)
        {
            if (ids.Count == 0) return;
            if (ids.Count > 2000 || ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Count ||
                ids.Any(id => { Guid parsed; return !Guid.TryParse(id, out parsed) || parsed == Guid.Empty; }))
                throw new ReceptionFailure("Lote de confirmacao invalido.");
            var payload = ReceptionJson.Write(ids.Select(id => new AckDto { id = id }).ToArray());
            await SendAsync(HttpMethod.Post, AckRoute, payload, ct).ConfigureAwait(false);
        }
        private async Task<byte[]> SendAsync(HttpMethod method, string route, byte[] payload, CancellationToken ct)
        {
            int? status = null; string outcome = "failed"; var watch = Stopwatch.StartNew();
            try
            {
                var bearer = await token(ct).ConfigureAwait(false);
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
                using (var request = new HttpRequestMessage(method, "https://merchant-api.ifood.com.br" + route))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(25));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                    if (method == HttpMethod.Get) request.Headers.Add("x-polling-merchants", merchant.ToString("D"));
                    // No types/groups/categories filters: they can auto-ACK unselected events.
                    if (payload != null) { request.Content = new ByteArrayContent(payload); request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json"); }
                    using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                    {
                        status = (int)response.StatusCode;
                        bool accepted = method == HttpMethod.Get ? status == 200 || status == 204 : status == 202;
                        if (!accepted)
                        {
                            int retry = 0;
                            if (response.Headers.RetryAfter != null)
                            {
                                var span = response.Headers.RetryAfter.Delta ?? (response.Headers.RetryAfter.Date.HasValue ? response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow : TimeSpan.Zero);
                                retry = (int)Math.Max(0, Math.Min(int.MaxValue, Math.Ceiling(span.TotalSeconds)));
                            }
                            throw new ReceptionFailure("iFood HTTP " + status + " em " + route, status == 429 || status >= 500, retry);
                        }
                        if (status == 204 || method == HttpMethod.Post) { outcome = status == 204 ? "empty" : "ack-accepted"; return new byte[0]; }
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var buffer = new MemoryStream())
                        {
                            var chunk = new byte[4096]; int read;
                            while ((read = await input.ReadAsync(chunk, 0, chunk.Length, deadline.Token).ConfigureAwait(false)) > 0)
                            { if (buffer.Length + read > 2000000) throw new ReceptionFailure("Resposta de eventos excedeu 2 MB."); buffer.Write(chunk, 0, read); }
                            if (buffer.Length == 0) throw new ReceptionFailure("HTTP 200 sem lista de eventos.");
                            outcome = "received"; return buffer.ToArray();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); throw new ReceptionFailure("Tempo de rede esgotado na recepcao.", true); }
            catch (HttpRequestException) { throw new ReceptionFailure("Falha de rede na recepcao.", true); }
            finally { try { if (evidence != null) evidence(new AuthenticationEvidence(method.Method, route, status, watch.ElapsedMilliseconds, outcome)); } catch { } }
        }
        public void Dispose() { http.Dispose(); }
#pragma warning disable CS0649
        [DataContract] private sealed class EventDto
        {
            [DataMember(IsRequired = true)] public string id;
            [DataMember(IsRequired = true)] public string orderId;
            [DataMember(IsRequired = true)] public string merchantId;
            [DataMember(IsRequired = true)] public string code;
            [DataMember(IsRequired = true)] public string fullCode;
            [DataMember(IsRequired = true)] public string createdAt;
        }
#pragma warning restore CS0649
        [DataContract] private sealed class AckDto { [DataMember] public string id; }
    }

    public sealed class ReceptionCycle
    {
        public bool Polled { get; internal set; }
        public int Received { get; internal set; }
        public int Added { get; internal set; }
        public int Acknowledged { get; internal set; }
        public string Message { get; internal set; }
    }
    public sealed class EventReceiver
    {
        private readonly EventsClient api;
        private readonly EventJournal journal;
        private readonly Func<DateTimeOffset> now;
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private int failures;
        public bool Stopped { get; private set; }
        public EventReceiver(EventsClient client, EventJournal store, Func<DateTimeOffset> clock = null)
        { api = client; journal = store; now = clock ?? (() => DateTimeOffset.UtcNow); }
        public async Task<ReceptionCycle> RunCycleAsync(CancellationToken ct)
        {
            await serial.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (Stopped) throw new ReceptionFailure("Recepcao interrompida. Salve o resultado para analise.");
                if (now() < journal.NextAttemptUtc) return new ReceptionCycle { Message = "Aguardando intervalo de consulta." };
                // Persist the throttle before HTTP, including across closing/reopening the receiver.
                journal.Schedule(now().AddSeconds(30));
                try
                {
                    int recovered = await AckPendingAsync(ct).ConfigureAwait(false);
                    var events = await api.PollAsync(ct).ConfigureAwait(false);
                    int added = events.Count == 0 ? 0 : journal.Stage(events);
                    int acknowledged = await AckPendingAsync(ct).ConfigureAwait(false);
                    failures = 0;
                    return new ReceptionCycle { Polled = true, Received = events.Count, Added = added, Acknowledged = acknowledged + recovered,
                        Message = events.Count == 0 ? "Conectado. Aguardando pedido de teste." : "Eventos recebidos e gravados. Separacao ainda nao executada." };
                }
                catch (ReceptionFailure ex)
                {
                    failures++;
                    if (!ex.Retryable || failures >= 3) { Stopped = true; throw; }
                    int seconds = Math.Max(ex.RetrySeconds, 30 * (1 << failures));
                    journal.Schedule(now().AddSeconds(seconds));
                    return new ReceptionCycle { Message = ex.Message + " Nova tentativa em " + seconds + " s (limite: 3 falhas consecutivas)." };
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { Stopped = true; throw; }
            finally { serial.Release(); }
        }
        private async Task<int> AckPendingAsync(CancellationToken ct)
        {
            int count = 0; var pending = journal.PendingIds();
            for (int offset = 0; offset < pending.Count; offset += 2000)
            {
                var batch = pending.Skip(offset).Take(2000).ToList();
                await api.AcknowledgeAsync(batch, ct).ConfigureAwait(false);
                journal.MarkAckAccepted(batch); count += batch.Count;
            }
            return count;
        }
    }
}
