using LealInfoPDV;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
static class Program
{
 const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,S=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
 static readonly Assembly A=typeof(MainForm).Assembly;static int checks;
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
 static object Call(string cls,string method,params object[] args)=>A.GetType("LealInfoPDV."+cls)!.GetMethods(S).Single(x=>x.Name==method && x.GetParameters().Length==args.Length).Invoke(null,args)!;
 static object Prop(object x,string name)=>x.GetType().GetProperty(name,I)!.GetValue(x)!;
 static object? Sql(string sql){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
 static long Count(string table)=>Convert.ToInt64(Sql("SELECT COUNT(*) FROM "+table));
 static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
 static T Find<T>(Control c,string name)where T:Control=>All(c).OfType<T>().Single(x=>x.Name==name);
 static Button Button(Form f,string text)=>All(f).OfType<Button>().Single(x=>x.Text==text);
 static void Pump(){Application.DoEvents();Thread.Sleep(30);Application.DoEvents();}
 static void Money(NumericUpDown field,string input){field.Focus();field.Select(0,field.Text.Length);foreach(char ch in input)SendMessage(field.Controls.OfType<TextBox>().Single().Handle,0x102,(IntPtr)ch,IntPtr.Zero);}
 static void Grid(Form f,GlassStoredProject p,int expected)
 {
  var grid=All(f).OfType<DataGridView>().Single();var overview=Find<Label>(f,"GlassFinanceOverview");Check(!grid.Bounds.IntersectsWith(overview.Bounds),"Resumo NAO encobre grade: ordem Dock correta");using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=(string)Call("GlassOperationsStore","HistorySql",p.Id);using var r=q.ExecuteReader();var ids=new List<long>();while(r.Read())ids.Add(r.GetInt64(0));
  Check(grid.Rows.Count==expected && ids.Count==expected,"Historico exatamente "+expected+" linhas independentes");
  var rows=grid.Rows.Cast<DataGridViewRow>().ToArray();Check(rows.Select(x=>Convert.ToInt64(x.Cells["ID"].Value)).SequenceEqual(ids) && ids.Distinct().Count()==expected,"Todos IDs do banco aparecem UMA UNICA VEZ na grade");
  if(expected==3){Check(rows.Select(x=>decimal.Parse(Convert.ToString(x.Cells["Valor"].Value)!.Replace("R$","").Trim(),CultureInfo.InvariantCulture)).SequenceEqual(new[]{20m,60m,50m}),"Valores reais50/60/20 em tres linhas");Check(rows.Select(x=>Convert.ToString(x.Cells["Tipo"].Value)).SequenceEqual(new[]{"SAÍDA","ENTRADA","ENTRADA"}),"Tipos de tres movimentos preservados");Check(rows.All(x=>!string.IsNullOrWhiteSpace(Convert.ToString(x.Cells["Data"].Value)) && !string.IsNullOrWhiteSpace(Convert.ToString(x.Cells["Hora"].Value)) && Convert.ToString(x.Cells["Descrição"].Value)!.Contains(p.Id)),"Data hora origem projeto e cliente preservados");Check(grid.DisplayedRowCount(false)>=3,"Tres linhas efetivamente visiveis na janela");}
 }
 static void Finance(MainForm main,GlassStoredProject p,bool register)
 {
  var examples=new[]{("50,00",50m,"ENTRADA"),("60,00",60m,"ENTRADA"),("20,00",20m,"SAÍDA")};int index=0,stage=0;var watch=Stopwatch.StartNew();using var timer=new System.Windows.Forms.Timer{Interval=80};
  timer.Tick+=(_,_)=>{
   if(watch.ElapsedMilliseconds>25000)throw new Exception("Timeout financeiro");var f=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="FLUXO DE CAIXA — VIDRAÇARIA");var e=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="Nova movimentação manual");
   if(stage==0 && f!=null){Grid(f,p,register?0:3);if(!register){timer.Stop();f.Close();return;}stage=1;Button(f,"NOVO").PerformClick();}
   else if(stage==1 && e!=null){var field=All(e).OfType<NumericUpDown>().Single();Check(field.Name=="GlassCashAmount","Financeiro reutiliza controle brasileiro ja aprovado");
    if(index==0){foreach(string culture in new[]{"pt-BR","en-US","de-DE"}){CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);foreach(var sample in new[]{("50,00",50m),("60,00",60m),("20,00",20m),("100,00",100m),("100,50",100.5m),("10050",100.5m),("0",0m)}){Money(field,sample.Item1);Check(field.Value==sample.Item2 && field.Text==sample.Item2.ToString("C2",CultureInfo.GetCultureInfo("pt-BR")).Replace('\u00a0',' '),"Moeda financeira teclado "+culture+" "+sample.Item1);}field.Text="";Check(field.Value==0,"Limpar zera sem restaurar valor antigo");Money(field,"100");SendMessage(field.Controls.OfType<TextBox>().Single().Handle,0x102,(IntPtr)'\b',IntPtr.Zero);Check(field.Value==0.10m,"Backspace e nova digitacao financeira");}CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");}
    Money(field,examples[index].Item1);Check(field.Value==examples[index].Item2,"Entrada explicita financeira "+examples[index].Item1);All(e).OfType<ComboBox>().Single().SelectedItem=examples[index].Item3;stage=2;Button(e,"SALVAR").PerformClick();}
   else if(stage==2){var h=FindWindow("#32770","Confirmar movimentação");if(h!=IntPtr.Zero){stage=3;PostMessage(h,0x111,(IntPtr)6,IntPtr.Zero);}}
   else if(stage==3 && e==null && f!=null){Grid(f,p,index+1);index++;if(index<3){stage=1;Button(f,"NOVO").PerformClick();}else{timer.Stop();f.Close();}}
  };timer.Start();typeof(MainForm).GetMethod("ShowFinance",I)!.Invoke(main,new object[]{p});Check(!register || index==3,"Tres operacoes legitimas pela MESMA janela financeira");
 }
 static void State(GlassStoredProject p){var s=Call("GlassOperationsStore","Read",p);Check((decimal)Prop(s,"Received")==110m && (decimal)Prop(s,"Expenses")==20m && (decimal)Prop(s,"Remaining")==22m && (string)Prop(s,"Status")=="PARCIALMENTE PAGO","50 + 60 - 20: recebido110 saidas20 liquido90 aReceber22 de132");var t=Call("CashFlow","ReadSummary",DateTime.Today,DateTime.Today.AddDays(1));Check((decimal)Prop(t,"Final")==90m,"Saldo real do Caixa 90 sem mudar matematica");}
 [STAThread]static void Main(string[] args)
 {
  Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Windows descartavel");Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Database.Initialize();
  if(args.Length==0){Sql("INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1');INSERT INTO customers(id,name) VALUES(993,'CLIENTE HISTORICO QA');SELECT 1");Auth.CreateUser("QA HISTORICO","qa_hist","Senha_Ficticia_123","ADMINISTRADOR","qa@example.invalid","");}
  Auth.Login("qa_hist","Senha_Ficticia_123");using var main=new MainForm();main.Show();Pump();((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",I)!.GetValue(main)!).Stop();typeof(MainForm).GetField("automaticBackupCompleted",I)!.SetValue(main,true);
  if(args.Length>0){var p=GlassProjectStore.Load(args[0]);State(p);Finance(main,p,false);Finance(main,p,false);Check(Count("cash_movements")==3 && Count("glass_cash_links_v1")==3,"Novo processo e reabrir Financeiro NAO duplicam");main.Close();return;}
  using var central=new GlassWorkshopForm();central.Show(main);Pump();Find<Button>(central,"LoadQuoteCustomers").PerformClick();Find<ComboBox>(central,"QuoteCustomer").SelectedIndex=1;Find<NumericUpDown>(central,"QuotePrice").Value=100;Find<NumericUpDown>(central,"QuoteAddition").Value=12;Find<TextBox>(central,"Description").Text="PROJETO HISTORICO QA";var saved=(GlassStoredProject)typeof(GlassWorkshopForm).GetMethod("SaveCurrentProject",I)!.Invoke(central,null)!;
  Check(Count("cash_movements")==0,"Orcamento salvar nao geram receita");Finance(main,saved,true);State(saved);Finance(main,saved,false);Finance(main,saved,false);Check(Count("cash_movements")==3 && Count("glass_cash_links_v1")==3,"Reabrir Financeiro nao duplica");
  var quote=(GlassQuote)Call("GlassOperationsStore","Quote",saved.Data);var snapshot=new GlassCommercialSnapshot(quote,993,"CLIENTE HISTORICO QA","","",DateTime.Now,Array.Empty<GlassMaterialLink>());GlassCommercialDocument.PdfBytes(snapshot);Call("GlassOperationsStore","Quote",saved.Data);var updated=GlassProjectStore.Save(saved.Data,saved);Check(Count("cash_movements")==3 && Count("sales")==0,"PDF recalculo salvar nao duplicam nem criam receita/venda");central.Close();main.Close();
  var psi=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};psi.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);psi.ArgumentList.Add(updated.Id);using var child=Process.Start(psi)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();Check(child.WaitForExit(60000),"Novo processo finalizou");Console.Write(stdout.GetAwaiter().GetResult());Console.Write(stderr.GetAwaiter().GetResult());Check(child.ExitCode==0,"Tres linhas e totais preservados apos fechar PDV e reabrir em NOVO PROCESSO");Console.WriteLine("PASS FINANCEIRO HISTORICO "+checks+" verificacoes");
 }
}
