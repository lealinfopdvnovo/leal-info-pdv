namespace LealInfoPDV;

internal static class GlassDocumentPartyEditor
{
    internal static GlassDocumentContext? Edit(Form owner,GlassDocumentContext context)
    {
        using var form=new Form{Name="GlassDocumentParties",Text="DADOS DESTE DOCUMENTO",ClientSize=new(720,640),MinimumSize=new(500,360),AutoScaleMode=AutoScaleMode.Dpi,StartPosition=FormStartPosition.CenterParent};
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new(16)};
        var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};fields.ColumnStyles.Add(new(SizeType.Percent,100));scroll.Controls.Add(fields);
        var values=new Dictionary<string,TextBox>();
        foreach(var party in new[]{("Company",context.Company),("Customer",context.Customer)})
        {
            GlassWorkshopForm.AddField(fields,party.Item1=="Company"?"EMPRESA — cópia para este documento":"CLIENTE — mantém o ID vinculado",new Label{AutoSize=true,Text="Editar aqui não altera o cadastro."});
            var p=party.Item2;
            foreach(var f in new[]{("Name","Nome / razão social",p.Name),("Document","CPF / CNPJ",p.Document),("Phone","Telefone",p.Phone),("Email","E-mail",p.Email),("Address","Endereço",p.Address),("CityState","Cidade / UF",p.CityState)})
            {var t=new TextBox{Name="GlassDocument"+party.Item1+f.Item1,Text=f.Item3,MaxLength=1200};values[party.Item1+f.Item1]=t;GlassWorkshopForm.AddField(fields,f.Item2,t);}
        }
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new(12)};
        var save=new SalesVisualButton{Name="ApplyGlassDocumentParties",Text="APLICAR NO DOCUMENTO",AutoSize=true,MinimumSize=new(225,42),DialogResult=DialogResult.OK};
        var cancel=new SalesVisualButton{Text="CANCELAR",MinimumSize=new(135,42),DialogResult=DialogResult.Cancel};actions.Controls.AddRange(new Control[]{save,cancel});form.Controls.Add(scroll);form.Controls.Add(actions);scroll.BringToFront();form.AcceptButton=save;form.CancelButton=cancel;SalesDialogTheme.Apply(form,"Futurista Azul",null);
        var area=Screen.FromControl(owner).WorkingArea;form.Size=new(Math.Min(form.Width,area.Width),Math.Min(form.Height,area.Height));
        if(form.ShowDialog(owner)!=DialogResult.OK)return null;
        GlassDocumentParty Read(string key,GlassDocumentParty p)=>p with{Name=values[key+"Name"].Text,Document=values[key+"Document"].Text,Phone=values[key+"Phone"].Text,Email=values[key+"Email"].Text,Address=values[key+"Address"].Text,CityState=values[key+"CityState"].Text};
        return context with{Company=Read("Company",context.Company),Customer=Read("Customer",context.Customer)};
    }
}
internal sealed class GlassDocumentTextEditor : Form
{
    internal GlassDocumentTextEditor(string kind,GlassDocumentContext context,GlassReceiptSource? receipt,Action<Form>? theme)
    {
        bool isReceipt=receipt!=null;Name=isReceipt?"GlassReceiptEditor":"GlassRevisionEditor";Text=kind;ClientSize=new(900,700);MinimumSize=new(560,420);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterParent;
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new(16)};
        var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};fields.ColumnStyles.Add(new(SizeType.Percent,100));scroll.Controls.Add(fields);
        var source=new TextBox{Name="GlassEditedDocumentSource",ReadOnly=true,Multiline=true,Height=130,ScrollBars=ScrollBars.Vertical,Text=$"Empresa: {context.Company.Name}\r\nCliente: {context.Customer.Name} • ID {context.Customer.Id}\r\nProjeto: {context.Project.Description} • revisão {context.ProjectRevision}\r\nMedidas: {context.Project.WidthMm} × {context.Project.HeightMm} mm • quantidade {context.Project.Quantity}\r\n"+(isReceipt?$"Recebimento real: {GlassBrazilianMoney.Format(receipt!.Amount)} • lançamento {receipt.MovementId}":$"Valor do projeto: {GlassBrazilianMoney.Format(context.CommercialValue)}")};
        GlassWorkshopForm.AddField(fields,"DADOS E VALORES VINCULADOS — preservados",source);
        var body=new TextBox{Name="GlassEditedDocumentText",Multiline=true,Height=245,MaxLength=12000,ScrollBars=ScrollBars.Vertical,Text=isReceipt?GlassDocumentTemplates.ReceiptDeclaration(receipt!):GlassDocumentTemplates.Revision(context)};
        var notes=new TextBox{Name="GlassEditedDocumentNotes",Multiline=true,Height=85,MaxLength=12000,ScrollBars=ScrollBars.Vertical,Text=isReceipt?"Observações do recebimento: nenhuma adicional informada.":"Condições de pagamento: "+context.FinancialCondition};
        var signatures=new TextBox{Name="GlassEditedDocumentSignatures",Multiline=true,Height=200,MaxLength=12000,ScrollBars=ScrollBars.Vertical,Text=GlassDocumentTemplates.Signatures(context)};
        GlassWorkshopForm.AddField(fields,isReceipt?"DECLARAÇÃO DO RECIBO — modelo editável":"REVISÃO / ALTERAÇÕES / PRAZOS — modelo editável",body);GlassWorkshopForm.AddField(fields,"OBSERVAÇÕES / CONDIÇÕES",notes);GlassWorkshopForm.AddField(fields,"ASSINATURAS / LOCAL E DATA",signatures);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new(12)};
        var editParties=new SalesVisualButton{Name="EditGlassDocumentParties",Text="EDITAR DADOS DO DOCUMENTO",AutoSize=true,MinimumSize=new(250,44)};
        editParties.Click+=(_,_)=>{
            var oldContext=context;var oldReceipt=receipt;var edited=GlassDocumentPartyEditor.Edit(this,context);if(edited==null)return;
            context=edited;if(receipt!=null)receipt=receipt with{Context=edited};
            if(signatures.Text==GlassDocumentTemplates.Signatures(oldContext))signatures.Text=GlassDocumentTemplates.Signatures(context);
            if(oldReceipt!=null&&body.Text==GlassDocumentTemplates.ReceiptDeclaration(oldReceipt))body.Text=GlassDocumentTemplates.ReceiptDeclaration(receipt!);
            source.Text=$"Empresa: {context.Company.Name}\r\nCliente: {context.Customer.Name} • ID {context.Customer.Id}\r\nProjeto: {context.Project.Description} • revisão {context.ProjectRevision}\r\nMedidas: {context.Project.WidthMm} × {context.Project.HeightMm} mm • quantidade {context.Project.Quantity}\r\n"+(receipt!=null?$"Recebimento real: {GlassBrazilianMoney.Format(receipt.Amount)} • lançamento {receipt.MovementId}":$"Valor do projeto: {GlassBrazilianMoney.Format(context.CommercialValue)}");
        };
        var preview=new SalesVisualButton{Name="PreviewEditedGlassDocument",Text="VISUALIZAR / IMPRIMIR / PDF",AutoSize=true,MinimumSize=new(280,44)};
        preview.Click+=(_,_)=>{try{var b=body.Text;var n=notes.Text;var s=signatures.Text;GlassLinkedDocument.Preview(this,()=>isReceipt?GlassLinkedDocument.EditedReceipt(receipt!,b,n,s):GlassLinkedDocument.Revision(context,b,n,s),theme);}catch(Exception e){MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
        var close=new SalesVisualButton{Text="FECHAR",MinimumSize=new(135,44),DialogResult=DialogResult.Cancel};actions.Controls.AddRange(new Control[]{editParties,preview,close});Controls.Add(scroll);Controls.Add(actions);scroll.BringToFront();CancelButton=close;theme?.Invoke(this);if(theme==null)SalesDialogTheme.Apply(this,"Futurista Azul",null);
        Shown+=(_,_)=>{var area=Screen.FromControl(Owner??this).WorkingArea;Size=new(Math.Min(Width,area.Width),Math.Min(Height,area.Height));};
    }
}
