using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
static class ProfileProbe
{
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]
 static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
 static void Viewport(Form form,Size size){
  if(!SetWindowPos(form.Handle,IntPtr.Zero,0,0,size.Width,size.Height,0x0016))throw new System.ComponentModel.Win32Exception();
  var clock=System.Diagnostics.Stopwatch.StartNew();
  while(clock.ElapsedMilliseconds<5000){Application.DoEvents();System.Threading.Thread.Sleep(4);}
  if(form.ClientSize!=size)throw new Exception("Requested viewport "+size+" but Windows rendered "+form.ClientSize);
  if(form.Opacity<0.99)throw new Exception("Login animation did not finish fading in");
 }
 const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly Assembly App=typeof(IPetExtension).Assembly;
 static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> Walk(Control root)=>root.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Walk(x)));
 static string State(){
  using var db=Database.Open();var tables=new List<string>();
  using(var command=db.CreateCommand()){command.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";using var reader=command.ExecuteReader();while(reader.Read())tables.Add(reader.GetString(0));}
  var data=new Dictionary<string,List<object?[]>>();
  foreach(var table in tables){using var command=db.CreateCommand();command.CommandText="SELECT * FROM \""+table.Replace("\"","\"\"")+"\" ORDER BY rowid";using var reader=command.ExecuteReader();var rows=new List<object?[]>();while(reader.Read()){var values=new object?[reader.FieldCount];for(int i=0;i<values.Length;i++)values[i]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(values);}data[table]=rows;}
  return JsonSerializer.Serialize(data);
 }
 static void SealChecks(string identity){
  bool expected=identity is "001" or "003";
  foreach(var size in (expected?new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)}:new[]{new Size(1366,768)})){
   using var login=new LoginForm{WindowState=FormWindowState.Normal,ClientSize=size};
   login.Show();Viewport(login,size);Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();
   var seals=login.Controls.Find("petCertificationSeal",true);
   Check(seals.Length==(expected?1:0),"seal ownership limited to Loja/Casa for "+identity);
   var exit=(Button)login.Controls.Find("btnSairLogin",true).Single();
   if(expected){
    var seal=(PictureBox)seals.Single();
    Check(seal.Parent!.GetType().Name=="GlassBackdropPanel"&&seal.BackColor==Color.Transparent,"seal inherits actual animated backdrop without opaque rectangle");
    var below=seal.Parent.PointToClient(exit.Parent!.PointToScreen(new Point(exit.Left+exit.Width/2,exit.Bottom)));
    Check(Math.Abs(seal.Left+seal.Width/2-below.X)<=1&&seal.Top>=below.Y,"seal centered below SAIR at "+size);
    Check(seal.Width==(int)Math.Round(96.2*login.DeviceDpi/96.0),"approved 30 percent size increase retained");
    using var image=new Bitmap(login.Width,login.Height);
    login.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
    var area=login.RectangleToClient(seal.RectangleToScreen(seal.ClientRectangle));
    int golden=0;
    for(int y=Math.Max(0,area.Top);y<Math.Min(image.Height,area.Bottom);y++)
     for(int x=Math.Max(0,area.Left);x<Math.Min(image.Width,area.Right);x++){var c=image.GetPixel(x,y);if(c.R>150&&c.G>90&&c.B<110)golden++;}
    Check(golden>50,"approved golden image actually renders in Windows");
    image.Save("pair-evidence/login-seal-"+identity+"-"+size.Width+".png");
   }
   object?[] keyArgs={new Message(),Keys.Escape};
   var handled=(bool)typeof(LoginForm).GetMethod("ProcessCmdKey",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(login,keyArgs)!;
   Check(handled&&login.DialogResult==DialogResult.Cancel,"ESC still closes login");
  }
  using(var login=new LoginForm{WindowState=FormWindowState.Normal,ClientSize=new Size(1366,768)}){
   login.Show();Application.DoEvents();Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();
   ((Button)login.Controls.Find("btnSairLogin",true).Single()).PerformClick();
   Check(login.DialogResult==DialogResult.Cancel,"SAIR button still closes login");
  }
  if(expected){
   var type=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LealInfoPDV.Pet.Profile").GetType("ProfileCertificationSeal",true)!;
   var pixelSide=type.GetMethod("PixelSide",BindingFlags.Static|BindingFlags.NonPublic)!;
   foreach(int dpi in new[]{96,120,144})Check((int)pixelSide.Invoke(null,new object[]{dpi})! ==(int)Math.Round(96.2*dpi/96.0),"seal DPI sizing verified at "+dpi);
  }
 }
 [STAThread]static int Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=3)return 91;
  var identity=args[0];var version=args[1];var module=Path.GetFullPath(args[2]);
  try{
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   Database.Initialize();if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");
   Check(Auth.Login("qa","qa-test-123!")!=null,"QA login retained");
   var folder=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"profile-probe-"+identity+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
   File.Copy(module,Path.Combine(folder,"Pet.dll"));var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module)));
   File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonSerializer.Serialize(new{Identity=identity,Version=version,ApiVersion=1,Assembly="Pet.dll",Sha256=hash}));
   var before=State();
   App.GetType("LealInfoPDV.PetRuntime",true)!.GetMethod("Load",Flags)!.Invoke(null,new object[]{folder,identity});
   Check((string?)App.GetType("LealInfoPDV.PetRuntime",true)!.GetProperty("CurrentVersion",Flags)!.GetValue(null)==version,"actual profile "+identity+" version "+version+" loads against current common build");
   using(var main=new MainForm()){
    const BindingFlags p=BindingFlags.Instance|BindingFlags.NonPublic;
    ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",p)!.GetValue(main)!).Stop();
    typeof(MainForm).GetField("automaticBackupCompleted",p)!.SetValue(main,true);
    var handlers=(EventHandlerList)typeof(Component).GetProperty("Events",p)!.GetValue(main)!;
    var key=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(x=>x.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
    var shown=handlers[key];handlers.RemoveHandler(key,shown);
    main.Show();Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();
    Check(main.Text.Contains("PET V"+version),"native main form shows independent profile version for "+identity);
    if(identity is "001" or "003")Check(Walk(main).Count(x=>x.Text=="VIDRAÇARIA")==1,"approved business extension remains available for "+identity);
    Directory.CreateDirectory("pair-evidence");
    using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("pair-evidence/profile-"+identity+"-Windows.png");}
    main.Close();
   }
   SealChecks(identity);
   Check(State()==before,"all QA database rows preserved by profile load and native main form");
   using(var updates=new LealInfo.Components.ComponentUpdateForm(new LealInfo.Components.Installed("10.463",version,identity),new LealInfo.Components.Selection(Array.Empty<LealInfo.Components.Release>(),Array.Empty<string>()),_=>Task.CompletedTask)){
    updates.Text="VALIDAÇÃO ISOLADA — central integrada ao PDV atual";
    updates.Show();Application.DoEvents();
    using(var image=new Bitmap(updates.Width,updates.Height)){updates.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("pair-evidence/central-"+identity+"-Windows.png");}
    updates.Close();
   }
   File.WriteAllText("pair-evidence/profile-"+identity+".json",JsonSerializer.Serialize(new{Pass=true,Identity=identity,Version=version,ModuleSha256=hash,ActualProfileSource=true,OperationalCustomer=false,Published=false}));
   return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
}
