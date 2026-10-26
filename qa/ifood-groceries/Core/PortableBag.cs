#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace LealInfo.IFood.Groceries
{
    // One parser shared by the Framework Windows host and the .NET 8 domain adapter.
    [DataContract] public sealed class BagItem
    {
        [DataMember(IsRequired=true)] public Guid Id { get; internal set; }
        [DataMember(IsRequired=true)] public string Name { get; internal set; }
        [DataMember(IsRequired=true)] public int ReportedQuantity { get; internal set; }
        [DataMember(IsRequired=true)] public bool Unavailable { get; internal set; }
        [DataMember(IsRequired=true)] public decimal UnitCents { get; internal set; }
        [DataMember(IsRequired=true)] public decimal GrossCents { get; internal set; }
        public int AvailableQuantity { get { return Unavailable ? 0 : ReportedQuantity; } }
    }
    [DataContract] public sealed class BagBenefit
    {
        [DataMember(IsRequired=true)] public string Target { get; internal set; }
        [DataMember] public string TargetId { get; internal set; }
        [DataMember(IsRequired=true)] public string Liability { get; internal set; }
        [DataMember(IsRequired=true)] public decimal Cents { get; internal set; }
    }
    [DataContract] public sealed class BagOrder
    {
        [DataMember(IsRequired=true)] public Guid Id { get; internal set; }
        [DataMember(IsRequired=true)] public Guid MerchantId { get; internal set; }
        [DataMember(IsRequired=true)] public string Operation { get; internal set; }
        [DataMember(IsRequired=true)] public string Scheduling { get; internal set; }
        [DataMember] public string WindowFrom { get; internal set; }
        [DataMember] public string WindowTo { get; internal set; }
        [DataMember(IsRequired=true)] public BagItem[] Items { get; internal set; }
        [DataMember(IsRequired=true)] public BagBenefit[] Benefits { get; internal set; }
        [DataMember(IsRequired=true)] public decimal BagGrossCents { get; internal set; }
        [DataMember(IsRequired=true)] public decimal PaymentCents { get; internal set; }
    }
    public static class PortableBag
    {
        public static BagOrder Parse(string json, Guid expectedOrder, Guid merchant)
        {
            try
            {
                if (expectedOrder==Guid.Empty || merchant==Guid.Empty || json==null || Encoding.UTF8.GetByteCount(json)>1048576) throw Invalid();
                byte[] bytes=new UTF8Encoding(false,true).GetBytes(json);
                // Enforce single-root JSON as well as strict member types.
                ReceptionJson.ValidateDocument(bytes);
                var document=new XmlDocument { XmlResolver=null };
                using(var input=new MemoryStream(bytes))
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(input,new XmlDictionaryReaderQuotas {
                    MaxDepth=32,MaxStringContentLength=1048576,MaxArrayLength=100000,MaxBytesPerRead=4096,MaxNameTableCharCount=16384 })) document.Load(reader);
                XmlElement root=document.DocumentElement;Duplicates(root);
                if(Id(root,"id")!=expectedOrder || Id(Field(root,"merchant"),"id")!=merchant || !Bool(root,"test") || Text(root,"category")!="GROCERY") throw Invalid();
                var bag=Field(root,"bag");var list=Array(Field(bag,"items"));if(list.Count>500)throw Invalid();
                var items=new List<BagItem>();var ids=new HashSet<Guid>();
                foreach(var element in list)
                {
                    Guid id=Id(element,"uniqueId");if(!ids.Add(id))throw Invalid();
                    bool unavailable=Bool(element,"unavailable");int quantity;
                    var value=Field(element,"quantity");
                    if(Type(value)!="number" || !int.TryParse(value.InnerText,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out quantity) || quantity<0 || quantity>100000 || (!unavailable && quantity==0))throw Invalid();
                    if(Bool(Field(element,"product"),"isVariableWeight"))throw new ReceptionFailure("Produtos de peso variavel ainda nao sao suportados neste teste.");
                    var prices=Field(element,"prices");items.Add(new BagItem { Id=id,Name=Text(element,"name"),ReportedQuantity=quantity,Unavailable=unavailable,
                        UnitCents=Money(Field(prices,"unitValue")),GrossCents=Money(Field(prices,"grossValue")) });
                }
                var mode=Field(root,"operationMode");string operation=Text(mode,"type");
                if(operation!="DELIVERY" && operation!="TAKEOUT")throw Invalid();
                string scheduling=Text(mode,"schedulingType");if(scheduling!="IMMEDIATE" && scheduling!="TIME_SLOT")throw Invalid();
                var route=Field(mode,operation=="TAKEOUT"?"takeout":"delivery");
                var time=Optional(route,operation=="TAKEOUT"?"takeoutTime":"deliveryTime");
                string from=null,to=null;
                if(time!=null && Type(time)=="object")
                {
                    var window=Optional(time,"window");if(window!=null && Type(window)=="object")
                    {
                        from=Text(window,"from");to=Text(window,"to");
                        if(ReceptionJson.Timestamp(from)>=ReceptionJson.Timestamp(to))throw Invalid();
                    }
                }
                if(scheduling=="TIME_SLOT" && from==null)throw Invalid();
                var benefits=new List<BagBenefit>();var benefit=Optional(root,"benefit");
                if(benefit!=null && Type(benefit)!="null")foreach(var b in Array(Field(benefit,"benefits")))foreach(var sponsor in Array(Field(b,"sponsorships")))
                {
                    var target=Optional(b,"targetId");benefits.Add(new BagBenefit { Target=Text(b,"target"),TargetId=target!=null && Type(target)=="string"?target.InnerText:null,
                        Liability=Text(sponsor,"liability"),Cents=Money(Field(sponsor,"amount")) });
                }
                decimal paid=0;foreach(var method in Array(Field(Field(root,"payment"),"methods")))paid=checked(paid+Money(Field(method,"amount")));
                return new BagOrder { Id=expectedOrder,MerchantId=merchant,Operation=operation,Scheduling=scheduling,WindowFrom=from,WindowTo=to,
                    Items=items.ToArray(),Benefits=benefits.ToArray(),BagGrossCents=Money(Field(Field(bag,"prices"),"grossValue")),PaymentCents=paid };
            }
            catch(ReceptionFailure){throw;}
            catch {throw Invalid();}
        }
        private static ReceptionFailure Invalid(){return new ReceptionFailure("Sacola final invalida: identidade, unavailable, quantidade, valor ou horario. Nenhuma importacao concluida.");}
        private static string Type(XmlElement e){return e.HasAttribute("type")?e.GetAttribute("type"):"string";}
        private static XmlElement Optional(XmlElement parent,string name)
        {
            if(Type(parent)!="object")throw Invalid();
            return parent.ChildNodes.OfType<XmlElement>().SingleOrDefault(e=>e.LocalName==name && e.NamespaceURI=="");
        }
        private static XmlElement Field(XmlElement e,string name){return Optional(e,name)??throwMissing();}
        private static XmlElement throwMissing(){throw Invalid();}
        private static string Text(XmlElement e,string name)
        {var v=Field(e,name);if(Type(v)!="string" || v.InnerText.Length==0 || v.InnerText.Length>1024)throw Invalid();return v.InnerText;}
        private static Guid Id(XmlElement e,string name)
        {Guid id;if(!Guid.TryParse(Text(e,name),out id)||id==Guid.Empty)throw Invalid();return id;}
        private static bool Bool(XmlElement e,string name)
        {var v=Field(e,name);if(Type(v)!="boolean" || (v.InnerText!="true" && v.InnerText!="false"))throw Invalid();return v.InnerText=="true";}
        private static List<XmlElement> Array(XmlElement e)
        {if(Type(e)!="array")throw Invalid();return e.ChildNodes.OfType<XmlElement>().ToList();}
        private static decimal Money(XmlElement e)
        {
            var v=Field(e,"value");decimal cents;
            if(Type(v)!="number" || !decimal.TryParse(v.InnerText,NumberStyles.Float,CultureInfo.InvariantCulture,out cents) || cents<0 || decimal.Truncate(cents)!=cents)throw Invalid();
            var currency=Optional(e,"currency");if(currency!=null && (Type(currency)!="string" || currency.InnerText!="BRL"))throw Invalid();return cents;
        }
        private static void Duplicates(XmlElement e)
        {
            if(Type(e)=="object")
            {
                var keys=new HashSet<string>(StringComparer.Ordinal);
                foreach(XmlElement child in e.ChildNodes.OfType<XmlElement>())
                {
                    // JSON keys not legal XML names use the mapped item/name representation.
                    string key=child.NamespaceURI=="item"?child.GetAttribute("item"):child.LocalName;
                    if(!keys.Add(key))throw Invalid();Duplicates(child);
                }
            }
            else if(Type(e)=="array")foreach(XmlElement child in e.ChildNodes.OfType<XmlElement>())Duplicates(child);
        }
    }

    public sealed class BagStore : IDisposable
    {
        private readonly object stateGate=new object();
        [DataContract] private sealed class Stored
        {
            [DataMember(IsRequired=true)] public int Version;
            [DataMember(IsRequired=true)] public Guid Merchant;
            [DataMember(IsRequired=true)] public List<StoredReceipt> Receipts;
        }
        [DataContract] private sealed class StoredReceipt
        {
            [DataMember(IsRequired=true)] public Guid EventId;
            [DataMember(IsRequired=true)] public BagOrder Order;
        }
        private readonly string path;private readonly Guid merchant;private readonly FileStream lease;private Stored state;
        public BagStore(string directory,Guid testMerchant)
        {
            if(testMerchant==Guid.Empty)throw new ArgumentException("Test merchant required");
            merchant=testMerchant;Directory.CreateDirectory(directory);path=Path.Combine(directory,"final-bags-v1.json");
            lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            try
            {
                if(File.Exists(path))
                {
                    if(new FileInfo(path).Length>16000000)throw new ReceptionFailure("Limite da fila de sacolas excedido.");
                    state=ReceptionJson.Read<Stored>(File.ReadAllBytes(path));
                    if(state.Version!=1 || state.Merchant!=merchant || state.Receipts==null || state.Receipts.Count>1000 ||
                        state.Receipts.Any(r=>r==null || r.EventId==Guid.Empty || r.Order==null || r.Order.MerchantId!=merchant || r.Order.Id==Guid.Empty || r.Order.Items==null || r.Order.Benefits==null) ||
                        state.Receipts.Select(r=>r.EventId).Distinct().Count()!=state.Receipts.Count)throw new ReceptionFailure("Fila de sacolas incompativel; preserve a pasta.");
                    foreach(var receipt in state.Receipts)ValidateOrder(receipt.Order);
                }
                else state=new Stored { Version=1,Merchant=merchant,Receipts=new List<StoredReceipt>() };
            }catch{lease.Dispose();throw;}
        }
        public BagOrder Find(Guid eventId,Guid orderId)
        { lock(stateGate) {
            var found=state.Receipts.SingleOrDefault(r=>r.EventId==eventId);if(found==null)return null;
            if(found.Order.Id!=orderId)throw new ReceptionFailure("Conflito de evento na fila de sacolas.");
            return Copy(found.Order);
        } }
        public bool Contains(Guid eventId,Guid orderId)
        {
            lock(stateGate)
            {
                var found=state.Receipts.SingleOrDefault(r=>r.EventId==eventId);
                if(found!=null && found.Order.Id!=orderId)throw new ReceptionFailure("Conflito de identidade na fila de sacolas.");
                return found!=null;
            }
        }
        public IList<BagOrder> Snapshot()
        { lock(stateGate) {return state.Receipts.GroupBy(r=>r.Order.Id).Select(g=>Copy(g.Last().Order)).ToList().AsReadOnly();} }
        public void Save(Guid eventId,BagOrder order)
        { lock(stateGate) {
            if(eventId==Guid.Empty || order==null || order.MerchantId!=merchant)throw new ReceptionFailure("Identidade invalida na gravacao da sacola.");
            ValidateOrder(order);
            if(Find(eventId,order.Id)!=null)return;
            if(state.Receipts.Count>=1000)throw new ReceptionFailure("Fila de sacolas cheia. Preserve os dados para analise.");
            var draft=new Stored {Version=1,Merchant=merchant,Receipts=new List<StoredReceipt>(state.Receipts)};
            draft.Receipts.Add(new StoredReceipt { EventId=eventId,Order=Copy(order) });
            var bytes=ReceptionJson.Write(draft);if(bytes.Length>16000000)throw new ReceptionFailure("Fila de sacolas excedeu 16 MB.");
            string temporary=path+".tmp";using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);state=draft;
        } }
        private static BagOrder Copy(BagOrder order){return ReceptionJson.Read<BagOrder>(ReceptionJson.Write(order));}
        private void ValidateOrder(BagOrder order)
        {
            if(order.Id==Guid.Empty || order.MerchantId!=merchant || order.Items==null || order.Items.Length>500 || order.Benefits==null ||
                (order.Operation!="DELIVERY" && order.Operation!="TAKEOUT") || (order.Scheduling!="IMMEDIATE" && order.Scheduling!="TIME_SLOT") ||
                !ValidMoney(order.BagGrossCents) || !ValidMoney(order.PaymentCents) ||
                order.Items.Any(i=>i==null || i.Id==Guid.Empty || string.IsNullOrEmpty(i.Name) || i.Name.Length>1024 || i.ReportedQuantity<0 || i.ReportedQuantity>100000 ||
                    (!i.Unavailable && i.ReportedQuantity==0) || !ValidMoney(i.UnitCents) || !ValidMoney(i.GrossCents)) ||
                order.Items.Select(i=>i.Id).Distinct().Count()!=order.Items.Length ||
                order.Benefits.Any(b=>b==null || string.IsNullOrEmpty(b.Target) || string.IsNullOrEmpty(b.Liability) || !ValidMoney(b.Cents)))
                throw new ReceptionFailure("Dados normalizados da sacola invalidos. Preserve a pasta para analise.");
            if((order.WindowFrom==null)!=(order.WindowTo==null) || (order.Scheduling=="TIME_SLOT" && order.WindowFrom==null))
                throw new ReceptionFailure("Janela de agendamento invalida na sacola persistida.");
            if(order.WindowFrom!=null && ReceptionJson.Timestamp(order.WindowFrom)>=ReceptionJson.Timestamp(order.WindowTo))
                throw new ReceptionFailure("Ordem da janela de agendamento invalida.");
        }
        private static bool ValidMoney(decimal value){return value>=0 && decimal.Truncate(value)==value;}
        public void Dispose(){lease.Dispose();}
    }

    public sealed class BagProcessor : IDisposable
    {
        private readonly EventJournal journal;private readonly BagStore store;private readonly HttpClient http;
        private readonly Func<CancellationToken,Task<string>> token;private readonly Action<AuthenticationEvidence> evidence;
        private readonly SemaphoreSlim serial=new SemaphoreSlim(1,1);
        public BagProcessor(EventJournal events,BagStore bags,Func<CancellationToken,Task<string>> tokenProvider,Action<AuthenticationEvidence> log,HttpMessageHandler testHandler=null)
        {journal=events;store=bags;token=tokenProvider;evidence=log;http=new HttpClient(testHandler??new HttpClientHandler {AllowAutoRedirect=false});http.Timeout=TimeSpan.FromSeconds(25);}
        public async Task<int> ProcessAsync(CancellationToken ct)
        {
            await serial.WaitAsync(ct).ConfigureAwait(false);try
            {
                int imported=0;
                foreach(var row in journal.Snapshot().Where(e=>e.AwaitingFinalBag && (!e.BusinessProcessed || !store.Contains(Guid.Parse(e.Id),Guid.Parse(e.OrderId)))).Take(10))
                {
                    ct.ThrowIfCancellationRequested();Guid eventId=Guid.Parse(row.Id),orderId=Guid.Parse(row.OrderId),merchant=Guid.Parse(row.MerchantId);
                    if(!store.Contains(eventId,orderId))
                    {var json=await FetchAsync(orderId,ct).ConfigureAwait(false);var bag=PortableBag.Parse(json,orderId,merchant);store.Save(eventId,bag);imported++;}
                    // Crash between these commits safely reuses the persisted bag without refetch.
                    journal.MarkBagPersisted(row.Id);
                }
                return imported;
            }finally{serial.Release();}
        }
        private async Task<string> FetchAsync(Guid orderId,CancellationToken ct)
        {
            const string route="/order/v1.0/orders/{id}/virtual-bag";int? status=null;string outcome="failed";var watch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct))
                using(var request=new HttpRequestMessage(HttpMethod.Get,"https://merchant-api.ifood.com.br"+route.Replace("{id}",orderId.ToString("D"))))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(25));request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await token(deadline.Token).ConfigureAwait(false));
                    using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token).ConfigureAwait(false))
                    {
                        status=(int)response.StatusCode;if(status!=200)throw new ReceptionFailure("iFood HTTP "+status+" em "+route+". Sacola permanece pendente; sem repeticao automatica.");
                        using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var buffer=new MemoryStream())
                        {
                            byte[] chunk=new byte[4096];int count;
                            while((count=await input.ReadAsync(chunk,0,chunk.Length,deadline.Token).ConfigureAwait(false))>0)
                            {if(buffer.Length+count>1048576)throw new ReceptionFailure("Sacola excedeu 1 MB.");buffer.Write(chunk,0,count);}
                            var body=new UTF8Encoding(false,true).GetString(buffer.ToArray());outcome="received";return body;
                        }
                    }
                }
            }
            catch(ReceptionFailure){throw;}
            catch(OperationCanceledException){ct.ThrowIfCancellationRequested();throw new ReceptionFailure("Tempo de leitura da sacola esgotado. Evento preservado.");}
            catch{throw new ReceptionFailure("Falha de rede ou formato na leitura da sacola. Evento preservado.");}
            finally{try{if(evidence!=null)evidence(new AuthenticationEvidence("GET",route,status,watch.ElapsedMilliseconds,outcome));}catch{}}
        }
        public void Dispose(){http.Dispose();serial.Dispose();}
    }
}
