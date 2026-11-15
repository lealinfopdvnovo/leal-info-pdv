using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace LealInfoPDV;

// Generic app projects are never rendered as the legacy two-leaf window.
internal sealed class AngraProjectForm : Form
{
 internal AngraProjectForm(GlassStoredProject saved)
 {
  Text="PROJETO DO APP — "+saved.Data.Description;StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(820,680);MinimumSize=new Size(560,420);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
  var p=JsonNode.Parse(saved.Data.AppProject!.Value.GetRawText())!.AsObject();var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18)};Controls.Add(layout);
  void Info(string text){layout.Controls.Add(new Label{Text=text,AutoSize=true,MaximumSize=new Size(740,0),Margin=new Padding(0,7,0,7)});}
  Info("TIPO: "+saved.Data.ServiceType+"\nMODELO: "+AngraProjectData.Text(p["model"])+"\nCLIENTE: "+saved.Data.Quote.CustomerName+"\nID: "+saved.Id+" • Revisão: "+saved.Revision);
  Info("Medidas informadas pelo profissional (sem inferir fabricação):\n"+string.Join("\n",(p["measures"] as JsonArray??new()).Select(m=>AngraProjectData.Text(m?["label"])+": "+AngraProjectData.Text(m?["value"])+" "+AngraProjectData.Text(m?["unit"])+" • "+AngraProjectData.Text(m?["mm"])+" mm")));
  var title=new TextBox{Text=AngraProjectData.Text(p["title"]),Width=740};var notes=new TextBox{Text=AngraProjectData.Text(p["configuration"]),Width=740,Height=90,Multiline=true,ScrollBars=ScrollBars.Vertical};Info("NOME DO PROJETO");layout.Controls.Add(title);Info("OBSERVAÇÕES DO SERVIÇO");layout.Controls.Add(notes);
  if(saved.Data.WidthMm>0&&saved.Data.HeightMm>0){var q=new GlassQuote(new(saved.Data.Description,saved.Data.WidthMm,saved.Data.HeightMm,saved.Data.Quantity),saved.Data.Quote.Price,saved.Data.Quote.ServiceValue,saved.Data.Quote.Discount,saved.Data.Quote.Addition);Info("Área unitária: "+q.Area.ToString("N2",GlassBrazilianMoney.Culture)+" m²\nValor por m²: "+GlassBrazilianMoney.Format(q.PricePerSquareMeter)+"\nServiço: "+GlassBrazilianMoney.Format(q.ServiceValue)+"\nDesconto: "+GlassBrazilianMoney.Format(q.Discount)+"\nAcréscimo: "+GlassBrazilianMoney.Format(q.Addition)+"\nTOTAL COMERCIAL: "+GlassBrazilianMoney.Format(q.Total)+"\nOrçamento não é pagamento.");}
  foreach(var draft in p["materialDrafts"] as JsonArray??new())Info("Material de campo a conciliar com produto do PDV: "+AngraProjectData.Text(draft?["name"])+" — "+AngraProjectData.Text(draft?["specification"]));
  if(p["photoAttachment"] is JsonObject photo){string sha=AngraProjectData.Text(photo["sha256"]),remoteId=AngraProjectData.Text(photo["projectId"]);if(Guid.TryParse(remoteId,out _)&&sha.Length==64){string file=AngraSyncService.PhotoPath(remoteId,sha);if(File.Exists(file)){var image=new PictureBox{Width=740,Height=260,SizeMode=PictureBoxSizeMode.Zoom};using(var stream=File.OpenRead(file))using(var original=Image.FromStream(stream))image.Image=new Bitmap(original);layout.Controls.Add(image);FormClosed+=(_,_)=>image.Image?.Dispose();}else Info("Foto vinculada; transferência ainda pendente. Reabra após a sincronização.");}}
  if(saved.Data.Cut!=null)Info("Plano de corte recebido: "+saved.Data.Cut.WidthMm+" × "+saved.Data.Cut.HeightMm+" mm • "+saved.Data.Cut.Pieces.Length+" peças. Dados estruturados preservados; nenhum algoritmo foi alterado.");
  var save=new SalesVisualButton{Text="SALVAR NOME / OBSERVAÇÕES",Width=420,Height=44};save.Click+=(_,_)=>{try{if(title.Text.Trim()=="")throw new InvalidOperationException("Informe o nome.");var current=GlassProjectStore.Load(saved.Id);if(current.Revision!=saved.Revision)throw new InvalidOperationException("O projeto mudou. Reabra antes de salvar; nenhuma versão foi substituída.");p["title"]=title.Text.Trim();p["configuration"]=notes.Text;var changed=current.Data with{Description=title.Text.Trim(),Quote=current.Data.Quote with{Service=notes.Text},AppProject=JsonSerializer.SerializeToElement(p)};saved=GlassProjectStore.Save(changed,current);MessageBox.Show(this,"Projeto salvo. Sincronização automática pendente; nenhuma receita ou venda criada.",Text);}catch(Exception e){MessageBox.Show(this,e.Message,Text);}};layout.Controls.Add(save);
  var close=new SalesVisualButton{Text="FECHAR",Width=180,Height=44,DialogResult=DialogResult.Cancel};close.Click+=(_,_)=>Close();layout.Controls.Add(close);CancelButton=close;SalesDialogTheme.Apply(this,"Futurista Azul",null);
 }
}
