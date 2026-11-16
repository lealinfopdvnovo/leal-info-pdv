using LealInfoPDV;using AngraQaIntegration;using System.Reflection;using System.Windows.Forms;
static class Program {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
 static void Sql(string sql){using var c=Database.Open();using var q=c.CreateCommand();q.CommandText=sql;q.ExecuteNonQuery();}
 static void Denied(Action a){try{a();}catch(Exception){return;}throw new Exception("Expected rejection");}
 [STAThread]static void Main(string[] args){
 Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Disposable runner only");Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Directory.CreateDirectory("integration-evidence");
 Database.Initialize();Sql("INSERT OR REPLACE INTO settings(key,value) VALUES('company_name','ANGRA VIDROS QA');INSERT INTO customers(name,phone) VALUES('Cliente QA','(24) 99999-1234');");
 Auth.CreateUser("Proprietário QA","owner","Synthetic-QA-Only!","ADMINISTRADOR","qa@example.invalid","");Auth.CreateUser("Funcionário QA","staff","Synthetic-QA-Only!","FUNCIONARIO","staff@example.invalid","");Auth.Login("owner","Synthetic-QA-Only!");
 using var form=new GlassWorkshopForm();form.Show();Application.DoEvents();QuoteWhatsApp.Attach(form);Check(form.Controls.Find("AngraQuoteWhatsApp",true).Length==0,"Applied without QA PET");
 Assembly.LoadFrom(Path.GetFullPath(args[0]));QuoteWhatsApp.Observe();QuoteWhatsApp.Attach(form);QuoteWhatsApp.Attach(form);Application.DoEvents();
 var button=(Button)form.Controls.Find("AngraQuoteWhatsApp",true).Single();Check(button.Visible,"Owner share button hidden");
 Denied(()=>QuoteWhatsApp.Prepare(form));All(form).OfType<Button>().Single(x=>x.Name=="LoadQuoteCustomers").PerformClick();var customers=All(form).OfType<ComboBox>().Single(x=>x.Name=="QuoteCustomer");customers.SelectedIndex=1;
 All(form).OfType<NumericUpDown>().Single(x=>x.Name=="QuotePrice").Value=150;All(form).OfType<NumericUpDown>().Single(x=>x.Name=="QuoteServiceValue").Value=50;All(form).OfType<TextBox>().Single(x=>x.Name=="Description").Text="Janela & orçamento #QA";
 var draft=QuoteWhatsApp.Prepare(form);Check(draft.Message.Contains("Cliente QA")&&draft.Message.Contains("Janela & orçamento #QA")&&draft.Message.Contains("230,00")&&draft.Message.Contains("NÃO É PAGAMENTO"),"Wrong commercial contents");
 var url=QuoteWhatsApp.CreateUrl(draft);Check(url.StartsWith("https://wa.me/5524999991234?text="),"Wrong WhatsApp recipient");Check(Uri.UnescapeDataString(url[(url.IndexOf("?text=")+6)..])==draft.Message,"Message encoding lost");
 foreach(var raw in new[]{"","123","https://evil.test","(24)99999-1234;calc"})Denied(()=>QuoteWhatsApp.NormalizePhone(raw));Check(QuoteWhatsApp.NormalizePhone("+55 (24) 99999-1234")=="5524999991234","Formatted phone rejected");
 string? opened=null;typeof(QuoteWhatsApp).GetField("launch",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,(Action<string>)(x=>opened=x));button.PerformClick();Check(opened!=null&&opened.Contains("wa.me/5524999991234"),"Real button did not launch correct conversation");
 form.Size=new System.Drawing.Size(1100,760);Application.DoEvents();using(var shot=new System.Drawing.Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new System.Drawing.Rectangle(System.Drawing.Point.Empty,shot.Size));shot.Save("integration-evidence/quote-whatsapp-button.png");}
 Auth.Login("staff","Synthetic-QA-Only!");QuoteWhatsApp.Attach(form);Check(!button.Visible&&!button.Enabled,"Employee has owner button");Denied(()=>QuoteWhatsApp.Prepare(form));Denied(()=>QuoteWhatsApp.CreateUrl(draft));
 Auth.Login("owner","Synthetic-QA-Only!");QuoteWhatsApp.Attach(form);Check(button.Visible,"Owner button did not return");Sql("UPDATE customers SET phone=''");Denied(()=>QuoteWhatsApp.CreateUrl(QuoteWhatsApp.Prepare(form)));
 using(var cn=Database.Open())using(var q=cn.CreateCommand()){q.CommandText="SELECT COUNT(*) FROM customers";Check(Convert.ToInt32(q.ExecuteScalar())==1,"Customer records modified");}
 form.Close();Console.WriteLine("PASS QA005 actual glass quote WhatsApp button: owner-only, live project/customer/prices, encoded correct destination, missing/invalid phone denied, no browser/send in CI");
 File.WriteAllText("integration-evidence/share-contract.txt","WhatsApp draft opens for explicit user confirmation. No FCM/server/API sending. No PDF auto-attachment. Physical Windows/WhatsApp receipt pending.");
 }
}
