using System.Reflection;
using LealInfoPDV;
internal static class Program
{
 [STAThread] static void Main()
 {
  Application.EnableVisualStyles(); Database.Initialize();
  var assembly=typeof(Database).Assembly;
  const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
  var mask=assembly.GetType("LealInfoPDV.CashAmountMask",true)!;
  var cash=assembly.GetType("LealInfoPDV.CashFlow",true)!;
  object Call(string name,params object[] args)=>cash.GetMethod(name,flags)!.Invoke(null,args)!;
  void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  using var field=new TextBox();mask.GetMethod("Attach",flags)!.Invoke(null,new object[]{field});
  void Key(char key)=>typeof(Control).GetMethod("OnKeyPress",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(field,new object[]{new KeyPressEventArgs(key)});
  void Type(string digits){field.SelectAll();foreach(var digit in digits)Key(digit);}
  foreach(var pair in new[]{("1","0,01"),("10","0,10"),("100","1,00"),("1000","10,00"),("10000","100,00"),("123456","1.234,56")})
  {Type(pair.Item1);Check(field.Text==pair.Item2,"Mascara: "+pair.Item1+" -> "+field.Text);Console.WriteLine(pair.Item1+" -> "+field.Text);}
  foreach(var expected in new[]{"123,45","12,34","1,23","0,12","0,01","0,00"})
  {Key('\b');Check(field.Text==expected,"Backspace: "+field.Text);}
  Type("123456");Check((decimal)Call("ParseAmount",field.Text)==1234.56m,"Conversao de milhar incorreta");
  Key('-');Key('a');Key(',');Check(field.Text=="1.234,56","Caracter invalido aceito");
  field.Text="-100";Check(field.Text=="1.234,56","Colagem negativa aceita");
  field.Text="100,50";Check(field.Text=="100,50","Colagem monetaria incorreta");
  object Summary()=>Call("ReadSummary",DateTime.Today,DateTime.Today.AddDays(1));
  decimal Final()=>(decimal)Summary().GetType().GetProperty("Final")!.GetValue(Summary())!;
  var before=Final();
  Type("10050");var entry=(long)Call("Register","ENTRADA","QA MASCARA ENTRADA",(decimal)Call("ParseAmount",field.Text));
  Check(Final()-before==100.50m,"Entrada incorreta");
  Type("5025");var exit=(long)Call("Register","SAÍDA","QA MASCARA SAIDA",(decimal)Call("ParseAmount",field.Text));
  Check(Final()-before==50.25m,"Efeito liquido incorreto");
  using(var db=Database.Open())using(var q=db.CreateCommand())
  {q.CommandText="SELECT amount FROM cash_movements WHERE id=$id";q.Parameters.AddWithValue("$id",entry);Check(Convert.ToDecimal(q.ExecuteScalar())==100.50m,"Entrada salva incorreta");q.Parameters["$id"].Value=exit;Check(Convert.ToDecimal(q.ExecuteScalar())==50.25m,"Saida salva incorreta");}
  Console.WriteLine("PASS: mascara, Backspace, selecao, caracteres, colagem, conversao 1234.56, banco Entrada +100.50 / Saida -50.25 / saldo liquido +50.25.");
 }
}
