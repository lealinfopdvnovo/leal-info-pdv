using System.Drawing;using System.Drawing.Drawing2D;using System.Reflection;using System.Text.Json;using System.Windows.Forms;
namespace AngraQaIntegration;
/// <summary>Photographed seven-step guide. No administrative calls or live data changes.</summary>
public class VisualEmployeeGuide:Form {
 readonly Form owner;readonly TableLayoutPanel root=new(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(16)};
 readonly TableLayoutPanel body=new(){Dock=DockStyle.Fill,Padding=new Padding(4)};
 readonly Panel photoPanel=new(){Dock=DockStyle.Fill,Padding=new Padding(12),BackColor=Color.FromArgb(238,248,255)};
 readonly Panel explanation=new(){Dock=DockStyle.Fill,Padding=new Padding(18),BackColor=Color.FromArgb(9,52,88)};
 readonly Label heading=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White};
 readonly TextBox instruction=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(220,240,252),BackColor=Color.FromArgb(9,52,88)};
 readonly FlowLayoutPanel nav=new(){Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,FlowDirection=FlowDirection.LeftToRight};
 readonly Label count=new(){AutoSize=true,ForeColor=Color.FromArgb(185,230,250),Padding=new Padding(14)};
 readonly Button previous=new(),next=new();readonly PhotoView photo=new(){Dock=DockStyle.Fill,Name="EmployeeTutorialPhotograph"};
 readonly List<Image> images=new();readonly List<RectangleF> targets=new();
 static readonly string[] Titles={"Ativação do proprietário","Cadastrar funcionário","Escolher permissões","Guardar o código de ativação","Ativar no celular","Perdeu o código?","Revisar e revogar acesso"};
 int step;bool released;public int Step=>step;public int Columns=>body.ColumnCount;public bool ImagesReady=>images.Count==7;public bool UsesApprovedGlow=>photo.HighlightRendered;
 public VisualEmployeeGuide(Form owner,Control target){
  this.owner=owner;Name="AngraEmployeeGuide";Text="Guia visual • Funcionários";AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;KeyPreview=true;
  Font=new Font("Segoe UI",11);BackColor=Color.FromArgb(7,31,54);ForeColor=Color.White;MinimumSize=new Size(620,460);Size=new Size(1120,760);
  root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
  var title=new Label{Text="GUIA VISUAL • FUNCIONÁRIOS",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",20,FontStyle.Bold),BackColor=Color.FromArgb(4,55,94),ForeColor=Color.White};root.Controls.Add(title,0,0);
  heading.Font=new Font("Segoe UI",14,FontStyle.Bold);root.Controls.Add(heading,0,1);root.Controls.Add(body,0,2);root.Controls.Add(nav,0,3);Controls.Add(root);
  var caption=new Label{Dock=DockStyle.Bottom,Height=46,TextAlign=ContentAlignment.MiddleCenter,Text="Capturas do fluxo real com dados de exemplo.\r\nCódigo ocultado por segurança.",ForeColor=Color.FromArgb(4,55,94),BackColor=Color.FromArgb(238,248,255),Font=new Font("Segoe UI",9)};
  photoPanel.Controls.Add(photo);photoPanel.Controls.Add(caption);explanation.Controls.Add(instruction);
  Style(previous,"◀ ANTERIOR",()=>MoveStep(-1));nav.Controls.Add(previous);nav.Controls.Add(count);Style(next,"PRÓXIMO ▶",()=>{if(step==6)Close();else MoveStep(1);});nav.Controls.Add(next);
  var replay=new Button();Style(replay,"VER NOVAMENTE",Restart);nav.Controls.Add(replay);var enlarge=new Button();Style(enlarge,"AMPLIAR IMAGEM",Enlarge);nav.Controls.Add(enlarge);var exit=new Button();Style(exit,"SAIR",Close);nav.Controls.Add(exit);
  photo.DoubleClick+=(_,_)=>Enlarge();photo.AccessibleName="Captura demonstrativa da etapa atual; use Ampliar imagem para visualizar maior";
  Resize+=(_,_)=>Adapt();KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)Close();else if(e.KeyCode==Keys.Right)MoveStep(1);else if(e.KeyCode==Keys.Left)MoveStep(-1);};
  owner.FormClosed+=OwnerClosed;FormClosed+=(_,_)=>ReleaseImages();
  LoadPhotos();Adapt();RenderStep();
  Shown+=(_,_)=>{var area=Screen.FromControl(owner).WorkingArea;MinimumSize=new Size(Math.Min(620,area.Width-12),Math.Min(460,area.Height-12));Bounds=new Rectangle(area.Left+12,area.Top+12,Math.Min(1120,area.Width-24),Math.Min(760,area.Height-24));Adapt();};
 }
 void Style(Button button,string label,Action click){button.Name="Guide"+label;button.Text=label;button.AutoSize=true;button.MinimumSize=new Size(110,44);button.Padding=new Padding(10,6,10,6);button.Margin=new Padding(4,8,4,8);button.BackColor=Color.FromArgb(0,118,178);button.ForeColor=Color.White;button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.AccessibleName=label;button.Click+=(_,_)=>click();}
 void LoadPhotos(){var asm=typeof(VisualEmployeeGuide).Assembly;using var metadata=asm.GetManifestResourceStream("Angra.StaffTutorial.GuidePhotos.json");if(metadata==null)throw new InvalidOperationException("Imagens do guia não foram incluídas nesta versão.");using var json=JsonDocument.Parse(metadata);
  for(int i=1;i<=7;i++){using var stream=asm.GetManifestResourceStream("Angra.StaffTutorial.step-"+i+".png");if(stream==null)throw new InvalidOperationException("Captura da etapa "+i+" indisponível.");using var source=Image.FromStream(stream);images.Add(new Bitmap(source));var a=json.RootElement.GetProperty(i.ToString()).GetProperty("rect").EnumerateArray().Select(x=>x.GetSingle()).ToArray();if(a.Length!=4||a.Any(x=>x<0||x>1)||a[2]<=0||a[3]<=0)throw new InvalidOperationException("Destaque inválido.");targets.Add(new RectangleF(a[0],a[1],a[2],a[3]));}
 }
 void Adapt(){if(IsDisposed||body.IsDisposed)return;bool narrow=ClientSize.Width<850;body.SuspendLayout();body.Controls.Clear();body.ColumnStyles.Clear();body.RowStyles.Clear();
  body.ColumnCount=narrow?1:2;body.RowCount=narrow?2:1;
  if(narrow){body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Percent,58));body.RowStyles.Add(new RowStyle(SizeType.Percent,42));body.Controls.Add(photoPanel,0,0);body.Controls.Add(explanation,0,1);}
  else{body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(photoPanel,0,0);body.Controls.Add(explanation,1,0);}body.ResumeLayout();photo.Invalidate();
 }
 public void MoveStep(int delta){step=Math.Clamp(step+delta,0,6);RenderStep();}public void Restart(){step=0;RenderStep();}
 void RenderStep(){if(released)return;heading.Text="PASSO "+(step+1)+" • "+Titles[step].ToUpperInvariant();count.Text=(step+1)+" de 7";
  instruction.Text=(EmployeeTutorial.Steps[step]+"\n\n"+(step==3?"O código da imagem foi ocultado. Use somente o código gerado para a pessoa cadastrada.":"As imagens são demonstrativas; o tutorial não preenche campos nem executa operações.")).Replace("\n","\r\n");
  previous.Enabled=step>0;next.Text=step==6?"CONCLUIR ✓":"PRÓXIMO ▶";if(images.Count==7){photo.Image=images[step];photo.Target=targets[step];photo.Invalidate();}
 }
 void Enlarge(){if(!ImagesReady||released)return;var viewer=new Form{Text="Imagem da etapa "+(step+1)+" • somente visualização",StartPosition=FormStartPosition.CenterParent,Width=1000,Height=760,BackColor=Color.FromArgb(7,31,54),KeyPreview=true};
  var view=new PictureBox{Dock=DockStyle.Fill,Image=images[step],SizeMode=PictureBoxSizeMode.Zoom};viewer.Controls.Add(view);viewer.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)viewer.Close();};viewer.Show(this);
 }
 void OwnerClosed(object? s,FormClosedEventArgs e)=>Close();
 void ReleaseImages(){if(released)return;released=true;owner.FormClosed-=OwnerClosed;photo.Image=null;foreach(var image in images)image.Dispose();images.Clear();}
 protected override void Dispose(bool disposing){if(disposing)ReleaseImages();base.Dispose(disposing);}
 sealed class PhotoView:Control {
  public Image? Image;public RectangleF Target;public bool HighlightRendered;Action<Graphics,GraphicsPath,Color,float,float>? glow;
  public PhotoView(){DoubleBuffered=true;BackColor=Color.FromArgb(238,248,255);}
  Action<Graphics,GraphicsPath,Color,float,float>? Glow(){if(glow!=null)return glow;var coach=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("AngraHelpPremium.Coach",false)).FirstOrDefault(t=>t!=null);
   if(coach==null){foreach(var asm in AppDomain.CurrentDomain.GetAssemblies())if(asm.GetManifestResourceNames().Contains("ANGRA.HelpPremium.Helper")){using var s=asm.GetManifestResourceStream("ANGRA.HelpPremium.Helper");if(s!=null)try{coach=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(s).GetType("AngraHelpPremium.Coach",false);}catch{}break;}}
   var m=coach?.GetMethod("Glow",BindingFlags.Static|BindingFlags.NonPublic);return glow=m?.CreateDelegate<Action<Graphics,GraphicsPath,Color,float,float>>();
  }
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Image==null||Width<1||Height<1)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
   float scale=Math.Min((Width-16f)/Image.Width,(Height-16f)/Image.Height);if(scale<=0)return;var box=new RectangleF((Width-Image.Width*scale)/2,(Height-Image.Height*scale)/2,Image.Width*scale,Image.Height*scale);e.Graphics.DrawImage(Image,box);
   var draw=Glow();if(draw==null)return;var r=new RectangleF(box.X+Target.X*box.Width,box.Y+Target.Y*box.Height,Target.Width*box.Width,Target.Height*box.Height);r.Inflate(3,3);
   using var ring=new GraphicsPath();ring.AddEllipse(r);draw(e.Graphics,ring,Color.FromArgb(255,62,70),2.3f,1);
   float tx=r.Left+r.Width*.72f,ty=r.Bottom;float tail=Math.Min(Height-6,ty+35);using var arrow=new GraphicsPath();arrow.AddLine(tx+22,tail,tx,ty);arrow.AddLine(tx,ty,tx-3,ty+12);arrow.StartFigure();arrow.AddLine(tx,ty,tx+13,ty+3);draw(e.Graphics,arrow,Color.FromArgb(0,218,255),2.2f,1);HighlightRendered=true;
  }
 }
}
