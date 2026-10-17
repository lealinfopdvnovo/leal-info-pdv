using System.Diagnostics;
namespace LealInfoPDV;
internal sealed class AngraPairingForm : Form
{
 private readonly AngraPairing service;
 private readonly Label status=new(){Name="AngraPairingStatus",AutoSize=true,MaximumSize=new Size(650,0)};
 private readonly Label validity=new(){AutoSize=true};
 private readonly TextBox code=new(){Name="AngraConnectionCode",ReadOnly=true,Width=580,Font=new Font("Consolas",17,FontStyle.Bold)};
 private readonly System.Windows.Forms.Timer timer=new(){Interval=10000};
 private bool busy;
 internal AngraPairingForm(){if(!Auth.IsAdmin)throw new InvalidOperationException("Somente o administrador pode configurar a integração.");service=new();Text="INTEGRAÇÃO ANGRA VIDROS";Name="AngraPairingForm";ClientSize=new Size(740,520);MinimumSize=new Size(560,420);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22),FlowDirection=FlowDirection.TopDown,WrapContents=false};Controls.Add(panel);
 panel.Controls.Add(new Label{Text=Text,Font=new Font("Segoe UI",19,FontStyle.Bold),AutoSize=true});panel.Controls.Add(status);panel.Controls.Add(new Label{AutoSize=true,MaximumSize=new Size(650,0),Text="No ANGRA VIDROS, abra INTEGRAÇÃO COM O PDV, informe o código e toque em CONECTAR AO PDV. O código vale por 10 minutos. Esta etapa vincula os aparelhos; os projetos e cadastros permanecem preservados."});
 void Button(string name,string text,Func<Task> action){var b=new SalesVisualButton{Name=name,Text=text,Width=580,Height=44};b.Click+=async(_,_)=>{if(busy)return;busy=true;try{await action();}catch(Exception e){MessageBox.Show(this,e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{busy=false;if(!IsDisposed)UpdateFields();}};panel.Controls.Add(b);}
 Button("GenerateAngraCode","GERAR / RENOVAR CÓDIGO DE CONEXÃO",async()=>{if(!await service.Generate()){if(MessageBox.Show(this,"Autorize este PDV uma única vez no serviço já utilizado pelo ANGRA VIDROS. Após autorizar, volte e gere o código.",Text,MessageBoxButtons.OKCancel,MessageBoxIcon.Information)==DialogResult.OK&&Uri.TryCreate(service.AuthorizationUrl,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.Host==new Uri(AngraSyncTransport.Origin).Host)Process.Start(new ProcessStartInfo(u.AbsoluteUri){UseShellExecute=true});}});
 panel.Controls.Add(code);panel.Controls.Add(validity);Button("CopyAngraCode","COPIAR CÓDIGO",()=>{if(service.Expires>DateTimeOffset.UtcNow&&!string.IsNullOrEmpty(service.Code))Clipboard.SetText(service.Code);return Task.CompletedTask;});
 Button("RevokeAngraLink","REVOGAR VÍNCULO",async()=>{if(MessageBox.Show(this,"Desconectar os aplicativos vinculados a este PDV e invalidar os códigos pendentes? Os dados serão preservados.",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)await service.Revoke();});Button("RefreshAngraStatus","ATUALIZAR STATUS",()=>service.Refresh());Button("CloseAngraPairing","FECHAR",()=>{Close();return Task.CompletedTask;});
 timer.Tick+=async(_,_)=>{if(busy)return;busy=true;try{await service.Refresh();if(!IsDisposed)UpdateFields();}finally{busy=false;}};Shown+=async(_,_)=>{await service.Refresh();if(!IsDisposed){UpdateFields();timer.Start();}};FormClosed+=(_,_)=>{timer.Dispose();};SalesDialogTheme.Apply(this,"Futurista Azul",null);
 }
 private void UpdateFields(){status.Text=service.Status;bool valid=service.Expires>DateTimeOffset.UtcNow;code.Text=valid?service.Code:"";validity.Text=valid?"Válido até "+service.Expires!.Value.LocalDateTime.ToString("HH:mm:ss")+" • Ao renovar, o código anterior é invalidado.":"Gere um código para conectar o aplicativo.";}
 protected override void Dispose(bool disposing){if(disposing&&!busy)service.Dispose();base.Dispose(disposing);}
}
