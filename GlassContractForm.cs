namespace LealInfoPDV;
internal sealed class GlassContractForm : Form
{
    private readonly GlassStoredProject project;
    private readonly ComboBox revisions=new(){Name="GlassContractRevisions",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top};
    private readonly Label status=new(){Name="GlassContractStatus",AutoSize=true,Dock=DockStyle.Top,Padding=new(10)};
    private readonly TextBox info=new(){Name="GlassContractSource",ReadOnly=true,Multiline=true,Height=175,Dock=DockStyle.Top,ScrollBars=ScrollBars.Vertical};
    private readonly Dictionary<string,TextBox> texts=new();
    private GlassContractContent content;
    private GlassStoredContract? selected;
    private long latest;
    private bool loading;
    internal GlassContractForm(GlassStoredProject saved,Action<Form>? theme)
    {
        project=saved;content=GlassDocumentStore.NewContract(saved);Name="GlassContracts";Text="CONTRATOS — VIDRAÇARIA";ClientSize=new(980,720);MinimumSize=new(600,420);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;ShowInTaskbar=false;
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new(14)};var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};fields.ColumnStyles.Add(new(SizeType.Percent,100));scroll.Controls.Add(fields);
        GlassWorkshopForm.AddField(fields,"DADOS VINCULADOS / REVISÃO",revisions);GlassWorkshopForm.AddField(fields,"",info);
        foreach(var field in new[]{("Object","OBJETO DO SERVIÇO"),("Value","VALOR / TEXTO COMERCIAL"),("Payment","CONDIÇÃO DE PAGAMENTO"),("Deadline","PRAZO"),("Installation","INSTALAÇÃO"),("Responsibilities","RESPONSABILIDADES"),("Notes","OBSERVAÇÕES")})
        {var text=new TextBox{Name="GlassContract"+field.Item1,Multiline=true,Height=100,MaxLength=12000,ScrollBars=ScrollBars.Vertical};texts.Add(field.Item1,text);GlassWorkshopForm.AddField(fields,field.Item2,text);}
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new(10)};
        void Button(string name,string label,Action action){var b=new SalesVisualButton{Name=name,Text=label,AutoSize=true,MinimumSize=new(155,42)};b.Click+=(_,_)=>{try{action();}catch(Exception e){MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}};actions.Controls.Add(b);}
        Button("RefreshGlassContractSource","CARREGAR DADOS ATUAIS",()=>{content=GlassDocumentStore.NewContract(project) with{Clauses=ReadClauses()};selected=null;ShowInfo();status.Text="Dados atuais carregados. Salvar criará nova revisão sem apagar as anteriores.";});
        Button("SaveGlassContract","SALVAR RASCUNHO",()=>Save(false));
        Button("FinalizeGlassContract","SALVAR FINALIZADO",()=>{if(MessageBox.Show(this,"Salvar uma revisão FINALIZADA com os textos revisados? As anteriores serão preservadas. Isto não registra pagamento.","Finalizar contrato",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)Save(true);});
        Button("PreviewGlassContract","VISUALIZAR / PDF / A4",()=>{if(selected==null||ReadClauses()!=selected.Content.Clauses||content.Context!=selected.Content.Context)throw new InvalidOperationException("Salve os textos antes de visualizar o documento. As revisões anteriores permanecem preservadas.");var revision=selected.Revision;GlassLinkedDocument.Preview(this,()=>GlassLinkedDocument.Contract(GlassDocumentStore.Contracts(project.Id).Single(x=>x.Revision==revision)),theme);});
        Button("CloseGlassContract","FECHAR",()=>{if(HasChanges() && MessageBox.Show(this,"Fechar descarta apenas o texto ainda não salvo. Continuar?",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;Close();});
        revisions.SelectedIndexChanged+=(_,_)=>{if(loading||revisions.SelectedItem is not RevisionChoice choice)return;if(HasChanges()&&MessageBox.Show(this,"Abrir outra revisão descarta textos ainda não salvos. Continuar?",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes){loading=true;revisions.SelectedItem=revisions.Items.Cast<RevisionChoice>().FirstOrDefault(x=>x.Document.Revision==selected?.Revision);loading=false;return;}Apply(choice.Document);};
        Controls.Add(scroll);Controls.Add(status);Controls.Add(actions);scroll.BringToFront();Reload(null);theme?.Invoke(this);
    }
    private sealed record RevisionChoice(GlassStoredContract Document){public override string ToString()=>"REVISÃO "+Document.Revision+" — "+Document.State+" — "+DateTime.Parse(Document.CreatedUtc).ToLocalTime().ToString("g",GlassBrazilianMoney.Culture);}
    private GlassContractClauses ReadClauses()=>new(texts["Object"].Text,texts["Value"].Text,texts["Payment"].Text,texts["Deadline"].Text,texts["Installation"].Text,texts["Responsibilities"].Text,texts["Notes"].Text);
    private bool HasChanges()=>ReadClauses()!=content.Clauses;
    private void Apply(GlassStoredContract? document)
    {
        selected=document;if(document!=null)content=document.Content;
        var c=content.Clauses;var values=new[]{c.Object,c.Value,c.Payment,c.Deadline,c.Installation,c.Responsibilities,c.Notes};int i=0;foreach(var text in texts.Values)text.Text=values[i++];ShowInfo();
        status.Text=document==null?"Novo contrato. Textos editáveis; salvar não registra receita, pagamento ou estoque.":$"REVISÃO {document.Revision} — {document.State}. Editar e salvar cria OUTRA revisão. Nenhuma revisão anterior é sobrescrita.";
    }
    private void ShowInfo(){var c=content.Context;info.Text=$"Empresa: {c.Company.Name} • {c.Company.Document}\r\nCliente: {c.Customer.Name} • ID {c.Customer.Id} • {c.Customer.Document}\r\nTelefone: {c.Customer.Phone} • Endereço: {c.Customer.Address}\r\nProjeto: {c.Project.Description} • ID {c.ProjectId} • revisão {c.ProjectRevision}\r\nModelo: {c.Project.Model} • Medidas: {c.Project.WidthMm} × {c.Project.HeightMm} mm • Quantidade: {c.Project.Quantity}\r\nValor comercial: {GlassBrazilianMoney.Format(c.CommercialValue)}\r\n{c.FinancialCondition}";}
    private void Reload(long? revision){var list=GlassDocumentStore.Contracts(project.Id);latest=list.Count==0?0:list[0].Revision;loading=true;revisions.Items.Clear();foreach(var document in list)revisions.Items.Add(new RevisionChoice(document));var choice=revisions.Items.Cast<RevisionChoice>().FirstOrDefault(x=>x.Document.Revision==(revision??latest));revisions.SelectedItem=choice;loading=false;Apply(choice?.Document);}
    private void Save(bool finalize){var saved=GlassDocumentStore.SaveContract(project,content with{Clauses=ReadClauses()},latest,finalize);Reload(saved.Revision);}
}
