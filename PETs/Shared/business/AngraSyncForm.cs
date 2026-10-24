using System.Diagnostics;
using System.Text.Json.Nodes;
namespace LealInfoPDV;

internal sealed class AngraSyncForm : Form
{
 private readonly Label status=new(){AutoSize=true,MaximumSize=new Size(760,0)};
 private readonly TextBox code=new(){ReadOnly=true,Width=340};
 private readonly ListBox conflicts=new(){Width=740,Height=140};
 private readonly System.Windows.Forms.Timer refresh=new(){Interval=1200};
 private readonly AngraSyncService service;
 private List<AngraLink> rows=new();
 internal AngraSyncForm()
 {
  if(!Auth.IsAdmin)throw new InvalidOperationException("Somente o administrador pode configurar a integração.");
  service=AngraSyncService.Current??throw new InvalidOperationException("Abra a integração pela tela principal do PDV.");
  Text="ANGRA VIDROS — INTEGRAÇÃO COM O APP";StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(820,650);MinimumSize=new Size(560,420);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
  var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(18)};Controls.Add(layout);
  layout.Controls.Add(status);layout.Controls.Add(new Label{AutoSize=true,MaximumSize=new Size(740,0),Text="Autorize este PDV uma vez e conecte o celular com o código. Depois, a sincronização é automática com os sistemas abertos. Orçamento não gera receita, venda ou estoque."});
  void Button(string text,Func<Task> action){var b=new SalesVisualButton{Text=text,Width=420,Height=44};b.Click+=async(_,_)=>{b.Enabled=false;try{await action();}catch(Exception e){MessageBox.Show(this,e.Message,Text);}finally{b.Enabled=true;RefreshState();}};layout.Controls.Add(b);}
  Button("CONECTAR / AUTORIZAR ESTE PDV",async()=>{await service.Connect();if(service.AuthorizationUrl!=""&&service.Store==null)Process.Start(new ProcessStartInfo(service.AuthorizationUrl){UseShellExecute=true});});
  Button("GERAR CÓDIGO PARA O CELULAR (10 MINUTOS)",async()=>{code.Text=await service.Ticket();});layout.Controls.Add(code);Button("COPIAR CÓDIGO",()=>{if(code.Text!="")Clipboard.SetText(code.Text);return Task.CompletedTask;});
  layout.Controls.Add(new Label{Text="CONFLITOS / PENDÊNCIAS — nenhuma versão é sobrescrita silenciosamente",AutoSize=true});layout.Controls.Add(conflicts);
  Button("REVISAR AS DUAS VERSÕES",()=>{if(conflicts.SelectedIndex<0)return Task.CompletedTask;var l=rows[conflicts.SelectedIndex];MessageBox.Show(this,"LOCAL:\n"+service.Store!.ReadLocal(l)?.ToJsonString(new(){WriteIndented=true})+"\n\nREMOTO:\n"+l.Conflict?.ToJsonString(new(){WriteIndented=true})+"\n\n"+l.Error,"Versões preservadas");return Task.CompletedTask;});
  Button("MANTER VERSÃO LOCAL DO PDV",()=>Resolve(false));Button("USAR VERSÃO REMOTA",()=>Resolve(true));
  Button("VINCULAR CLIENTE/FORNECEDOR AO CADASTRO EXISTENTE",()=>{if(conflicts.SelectedIndex<0)return Task.CompletedTask;var l=rows[conflicts.SelectedIndex];if(l.Entity is not("clients" or "suppliers"))throw new InvalidOperationException("Selecione conflito de cliente ou fornecedor.");using var dialog=new Form{Text="Vincular sem duplicar cadastro",Width=650,Height=180,StartPosition=FormStartPosition.CenterParent};var select=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,Height=40};var list=new List<(long Id,string Name)>();using(var db=Database.Open()){using var q=db.CreateCommand();q.CommandText="SELECT id,name FROM "+(l.Entity=="clients"?"customers":"suppliers")+" ORDER BY name";using var r=q.ExecuteReader();while(r.Read())list.Add((r.GetInt64(0),r.GetString(1)));}foreach(var row in list)select.Items.Add(row.Id+" • "+row.Name);var ok=new Button{Text="USAR CADASTRO SELECIONADO DO PDV",Dock=DockStyle.Bottom,Height=44,DialogResult=DialogResult.OK};dialog.Controls.Add(select);dialog.Controls.Add(ok);if(dialog.ShowDialog(this)==DialogResult.OK&&select.SelectedIndex>=0)service.Store!.LinkExisting(l.Entity,l.Id,list[select.SelectedIndex].Id);return Task.CompletedTask;});
  Button("FECHAR",()=>{Close();return Task.CompletedTask;});refresh.Tick+=(_,_)=>RefreshState();FormClosed+=(_,_)=>refresh.Dispose();refresh.Start();RefreshState();SalesDialogTheme.Apply(this,"Futurista Azul",null);
 }
 private Task Resolve(bool remote){if(conflicts.SelectedIndex>=0){var l=rows[conflicts.SelectedIndex];service.Store?.Resolve(l.Entity,l.Id,remote);}return Task.CompletedTask;}
 private void RefreshState(){status.Text=service.Status;int selected=conflicts.SelectedIndex;rows=service.Store?.Links().Where(x=>x.Conflict!=null||x.Error!="").ToList()??new();conflicts.Items.Clear();foreach(var l in rows)conflicts.Items.Add(l.Entity+" • "+l.Id[..8]+" • "+l.Error);if(selected>=0&&selected<conflicts.Items.Count)conflicts.SelectedIndex=selected;}
}
