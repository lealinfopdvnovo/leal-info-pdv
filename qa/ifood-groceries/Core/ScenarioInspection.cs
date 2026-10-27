#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace LealInfo.IFood.Groceries
{
    public enum InspectionState { Pass, Fail, Pending, Observed }
    public sealed class InspectionCriterion
    {
        public string Title { get; private set; }
        public InspectionState State { get; private set; }
        public string Detail { get; private set; }
        public bool QuantityCriterion { get; private set; }
        internal InspectionCriterion(string title,InspectionState state,string detail,bool quantity=false)
        {Title=title;State=state;Detail=detail;QuantityCriterion=quantity;}
        public string StatusText { get {return State==InspectionState.Pass?"APROVADO LOCAL":State==InspectionState.Fail?"DIVERGENTE":State==InspectionState.Pending?"PENDENTE":"OBSERVADO";} }
    }
    [DataContract] public sealed class ScenarioSelection
    {
        [DataMember(IsRequired=true)] public Guid OrderId { get; internal set; }
        [DataMember(IsRequired=true)] public Guid MerchantId { get; internal set; }
        [DataMember(IsRequired=true)] public Guid CancelItem { get; internal set; }
        [DataMember(IsRequired=true)] public Guid ThreeItem { get; internal set; }
        [DataMember(IsRequired=true)] public Guid OneItem { get; internal set; }
        public static ScenarioSelection Create(Guid order,Guid merchant,Guid cancel,Guid three,Guid one)
        {
            var result=new ScenarioSelection {OrderId=order,MerchantId=merchant,CancelItem=cancel,ThreeItem=three,OneItem=one};result.Validate();return result;
        }
        internal void Validate()
        {
            if(OrderId==Guid.Empty || MerchantId==Guid.Empty || CancelItem==Guid.Empty || ThreeItem==Guid.Empty || OneItem==Guid.Empty ||
                new[]{CancelItem,ThreeItem,OneItem}.Distinct().Count()!=3)throw new ReceptionFailure("Escolha tres produtos distintos para as acoes cancelar, separar 3 e separar 1.");
        }
        internal ScenarioSelection Copy(){return Create(OrderId,MerchantId,CancelItem,ThreeItem,OneItem);}
    }
    public sealed class ScenarioInspection
    {
        public IList<InspectionCriterion> Criteria { get; private set; }
        public bool QuantitiesMatch { get {return Criteria.Where(c=>c.QuantityCriterion).All(c=>c.State==InspectionState.Pass);} }
        private ScenarioInspection(List<InspectionCriterion> rows){Criteria=rows.AsReadOnly();}
        public static ScenarioInspection Inspect(BagOrder bag,ScenarioSelection selection)
        {
            if(bag==null || bag.Id==Guid.Empty || bag.MerchantId==Guid.Empty || bag.Items==null || bag.Benefits==null ||
                bag.Items.Any(i=>i==null || i.Id==Guid.Empty) || bag.Items.Select(i=>i.Id).Distinct().Count()!=bag.Items.Length)
                throw new ReceptionFailure("Identidade ou produtos invalidos para conferencia.");
            if(selection!=null){selection.Validate();if(selection.OrderId!=bag.Id || selection.MerchantId!=bag.MerchantId)throw new ReceptionFailure("Selecao pertence a outro pedido ou loja.");}
            var rows=new List<InspectionCriterion>();
            bool three=bag.Items.Length==3;
            rows.Add(new InspectionCriterion("Tres produtos no resultado",three?InspectionState.Pass:InspectionState.Fail,"Encontrados: "+bag.Items.Length+". O roteiro exige exatamente tres produtos.",true));
            AddLine(rows,bag,selection==null?Guid.Empty:selection.CancelItem,0,"Item 1 - indisponivel");
            AddLine(rows,bag,selection==null?Guid.Empty:selection.ThreeItem,3,"Item 2 - tres unidades");
            AddLine(rows,bag,selection==null?Guid.Empty:selection.OneItem,1,"Item 3 - uma unidade");
            rows.Add(new InspectionCriterion("Beneficios da sacola",InspectionState.Observed,
                bag.Benefits.Length+" patrocinio(s) informado(s). Presenca ou ausencia de beneficio final nao comprova aplicacao do cupom original."));
            rows.Add(new InspectionCriterion("Pedido original: 3 unidades de cada",InspectionState.Pending,"Exige evidencia do pedido original. A selecao de produtos descreve o plano, nao comprova quantidades originais."));
            rows.Add(new InspectionCriterion("Cupom aplicado",InspectionState.Pending,"Exige evidencia real do cupom no pedido original."));
            rows.Add(new InspectionCriterion("Solicitacao de cancelamento",InspectionState.Pending,"unavailable=true nao comprova o envio da solicitacao. Registrar a acao e seu resultado na ferramenta de separacao."));
            rows.Add(new InspectionCriterion("Video do fluxo completo",InspectionState.Pending,"Gravar criacao, recebimento e separacao reais DE TESTE."));
            rows.Add(new InspectionCriterion("Logs oficiais do aplicativo",InspectionState.Pending,"Exportar HTTP Requests no Developer Portal, do mesmo aplicativo usado no video."));
            return new ScenarioInspection(rows);
        }
        private static void AddLine(List<InspectionCriterion> rows,BagOrder bag,Guid id,int expected,string title)
        {
            if(id==Guid.Empty){rows.Add(new InspectionCriterion(title,InspectionState.Pending,"Identifique o produto desta acao pelo nome/ID, conforme o pedido original.",true));return;}
            var item=bag.Items.SingleOrDefault(i=>i.Id==id);
            if(item==null){rows.Add(new InspectionCriterion(title,InspectionState.Fail,"ID previsto ausente: "+id+". Ausencia de linha nao prova cancelamento.",true));return;}
            bool pass=expected==0?item.Unavailable && item.AvailableQuantity==0:!item.Unavailable && item.ReportedQuantity==expected && item.AvailableQuantity==expected;
            rows.Add(new InspectionCriterion(title,pass?InspectionState.Pass:InspectionState.Fail,
                "ID "+id+" | informada="+item.ReportedQuantity+" | unavailable="+item.Unavailable+" | disponivel="+item.AvailableQuantity+" | esperada="+expected,true));
        }
        public string Summary(BagOrder bag,ScenarioSelection selection,bool simulated)
        {
            // Re-evaluate instead of allowing a report for a different or subsequently changed bag.
            var current=Inspect(bag,selection);
            var text=new StringBuilder("LEAL INFO - RESUMO POR PEDIDO / CONFERENCIA R7\r\n");
            text.AppendLine("Origem: "+(simulated?"SIMULACAO LOCAL - SEM API, SEM HOMOLOGACAO":"SACOLA FINAL DE TESTE PERSISTIDA - CONFERENCIA LOCAL"));
            text.AppendLine("UTC: "+DateTimeOffset.UtcNow.ToString("O"));text.AppendLine("Pedido: "+bag.Id);text.AppendLine("Loja: "+bag.MerchantId);
            text.AppendLine("Resultado das quantidades: "+(current.QuantitiesMatch?"0/3/1 CONFORME PLANO LOCAL":"PENDENTE OU DIVERGENTE"));
            text.AppendLine("Nenhum resultado desta conferencia equivale a aprovacao do iFood.");
            text.AppendLine("Modalidade: "+bag.Operation+" | agendamento: "+bag.Scheduling+" | janela: "+bag.WindowFrom+" / "+bag.WindowTo);
            text.AppendLine("Valores informados em centavos: sacola="+bag.BagGrossCents.ToString(CultureInfo.InvariantCulture)+" | pagamento="+bag.PaymentCents.ToString(CultureInfo.InvariantCulture));
            foreach(var item in bag.Items)text.AppendLine("Item "+item.Id+" | qtd informada="+item.ReportedQuantity+" | unavailable="+item.Unavailable+" | qtd disponivel="+item.AvailableQuantity);
            foreach(var benefit in bag.Benefits)text.AppendLine("Beneficio "+benefit.Target+" | "+benefit.Liability+" | centavos="+benefit.Cents.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("CRITERIOS E EVIDENCIAS:");
            foreach(var row in current.Criteria)text.AppendLine(row.StatusText+" | "+row.Title+" | "+row.Detail);
            text.AppendLine("Resumo auxiliar; anexar videos e logs oficiais somente depois da validacao real.");
            return text.ToString();
        }
    }
    public sealed class ScenarioSelectionStore
    {
        [DataContract] private sealed class State
        {
            [DataMember(IsRequired=true)] public int Version;
            [DataMember(IsRequired=true)] public Guid Merchant;
            [DataMember(IsRequired=true)] public List<ScenarioSelection> Selections;
        }
        private readonly string path;private readonly Guid merchant;private State state;
        private readonly object gate=new object();
        // Host already owns the exclusive event/bag folder lease. No second writable host is allowed.
        public ScenarioSelectionStore(string directory,Guid testMerchant)
        {
            if(testMerchant==Guid.Empty)throw new ReceptionFailure("Loja de testes obrigatoria para plano.");
            merchant=testMerchant;Directory.CreateDirectory(directory);path=Path.Combine(directory,"scenario-selections-v1.json");
            if(File.Exists(path))
            {
                if(new FileInfo(path).Length>1048576)throw new ReceptionFailure("Arquivo de selecoes excedeu 1 MB.");
                state=ReceptionJson.Read<State>(File.ReadAllBytes(path));
                if(state.Version!=1 || state.Merchant!=merchant || state.Selections==null || state.Selections.Count>1000 ||
                    state.Selections.Any(s=>s==null || s.MerchantId!=merchant) || state.Selections.Select(s=>s.OrderId).Distinct().Count()!=state.Selections.Count)
                    throw new ReceptionFailure("Arquivo de selecoes incompativel. Preserve a pasta para analise.");
                foreach(var selection in state.Selections)selection.Validate();
            }
            else state=new State {Version=1,Merchant=merchant,Selections=new List<ScenarioSelection>()};
        }
        public ScenarioSelection Find(Guid order)
        {lock(gate){var found=state.Selections.SingleOrDefault(s=>s.OrderId==order);return found==null?null:found.Copy();}}
        public void Save(ScenarioSelection selection)
        {
            lock(gate)
            {
                if(selection==null)throw new ReceptionFailure("Identifique os produtos do cenario.");selection.Validate();
                if(selection.MerchantId!=merchant)throw new ReceptionFailure("Selecao de outra loja.");
                var next=state.Selections.Where(s=>s.OrderId!=selection.OrderId).Select(s=>s.Copy()).ToList();
                if(next.Count>=1000)throw new ReceptionFailure("Limite de 1000 planos de testes.");next.Add(selection.Copy());
                var draft=new State {Version=1,Merchant=merchant,Selections=next};var bytes=ReceptionJson.Write(draft);
                if(bytes.Length>1048576)throw new ReceptionFailure("Arquivo de selecoes excedeu 1 MB.");
                string temp=path+".tmp";using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);state=draft;
            }
        }
    }
}
