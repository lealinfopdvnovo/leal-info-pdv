using LealInfoPDV;
using System.Text.Json;
static class VisualOnly
{
    static readonly List<string> checks=new();
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
    static IEnumerable<Control> All(Control c)=>new[]{c}.Concat(c.Controls.Cast<Control>().SelectMany(All));
    [STAThread]static int Main(){
        if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Disposable Windows CI only");
        Directory.CreateDirectory("corrections-evidence");Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();
        try{
            Database.Initialize();Auth.CreateUser("QA VISUAL","visualparcial","QA-visual-123!","ADMINISTRADOR","qa@example.invalid","");Auth.Login("visualparcial","QA-visual-123!");
            using(var db=Database.Open())using(var q=db.CreateCommand()){q.CommandText="INSERT INTO products(id,name,price,stock,active,kitchen_preparation) VALUES(1,'REFRIGERANTE',5,100,1,1),(2,'ARROZ EM PREPARO',12,100,1,1)";q.ExecuteNonQuery();}
            var store=new KitchenStore();string id=store.EnsureReady();store.EnsureSeatingReady();
            KitchenOrder Submit(bool direct,long product,string name,decimal qty)=>store.Submit(id,new KitchenDraft(Guid.NewGuid(),"Mesa 01","","QA",DateTimeOffset.UtcNow,new[]{new KitchenItem(name,qty,"",product)},direct));
            var direct=Submit(true,1,"REFRIGERANTE",10);Submit(false,2,"ARROZ EM PREPARO",1);
            InternalWorkspace.Install();PremiumGlobalTheme.Install();using var form=new KitchenPartialPaymentForm(store.Quote(direct.Id));form.Show();Application.DoEvents();InternalWorkspace.Apply(form);PremiumGlobalTheme.Apply(form);Application.DoEvents();
            Check(KitchenPremiumStyle.Owns(form)&&form.BackColor==KitchenPremiumStyle.Background,"payment dialog retains kitchen black and gold after production idle theme");
            var grid=All(form).OfType<DataGridView>().Single();Check(grid.Rows.Count==2&&grid.BackgroundColor==KitchenPremiumStyle.Background,"consumed products remain visible on premium dark grid");
            Check(!All(form).OfType<Button>().Single(b=>b.Name=="KitchenConfirmFull").Enabled,"visual correction preserves total-payment preparation block");
            grid.Rows[0].Cells["Pay"].Value=true;grid.Rows[0].Cells["Quantity"].Value=2m;
            Check(All(form).Single(c=>c.Name=="KitchenPartialSummary").Text.Contains("10,00"),"visual correction preserves selected quantity and exact payment total");
            foreach(var button in All(form).OfType<Button>().Where(b=>b.Name.StartsWith("KitchenConfirm")||b.Name=="KitchenCancelPayment"))Check(button.Parent!.ClientRectangle.Contains(button.Bounds),"payment action fully visible: "+button.Name);
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("corrections-evidence/pagamento-parcial-premium.png");}
            form.Close();File.WriteAllText("corrections-evidence/visual.json",JsonSerializer.Serialize(new{success=true,checks}));Console.WriteLine("PASS PREMIUM VISUAL; FINANCIAL CORE REUSES APPROVED RUN 39706043559");Environment.Exit(0);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText("corrections-evidence/failure.txt",e.ToString());return 1;}
    }
}
