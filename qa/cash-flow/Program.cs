using System.Reflection;
using LealInfoPDV;
internal static class Program
{
    [STAThread] private static void Main()
    {
        Database.Initialize();Application.EnableVisualStyles();
        var assembly=typeof(Database).Assembly;var cash=assembly.GetType("LealInfoPDV.CashFlow",true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        object Call(string name,params object[] args)=>cash.GetMethod(name,flags)!.Invoke(null,args)!;
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        void Invalid(string text){try{Call("ParseAmount",text);throw new Exception("Valor inválido aceito: "+text);}catch(TargetInvocationException e) when(e.InnerException is ArgumentException){}}
        foreach(var text in new[]{"","0","-10,00","abc","0,001"})Invalid(text);
        Check((decimal)Call("ParseAmount","100,00")==100m,"Valor BR incorreto");
        using(var db=Database.Open())using(var q=db.CreateCommand())
        {
            q.CommandText="INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES($previous,'ENTRADA','QA SALDO INICIAL',200,NULL),($today,'ENTRADA','QA RECEBIMENTO EXISTENTE',1000,999999);";
            q.Parameters.AddWithValue("$previous",DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"));q.Parameters.AddWithValue("$today",DateTime.Today.ToString("yyyy-MM-dd HH:mm:ss"));q.ExecuteNonQuery();
        }
        object Summary()=>Call("ReadSummary",DateTime.Today,DateTime.Today.AddDays(1));
        decimal Value(object summary,string field)=>(decimal)summary.GetType().GetProperty(field)!.GetValue(summary)!;
        var before=Summary();Check(Value(before,"Opening")==200m&&Value(before,"Receipts")==1000m&&Value(before,"Final")==1200m,"Baseline incorreto ou recebimento duplicado");
        var entry=(long)Call("Register","ENTRADA","REFORÇO DE CAIXA",100m);
        Check(Value(Summary(),"Final")==1300m,"Entrada não somou 100");
        var exit=(long)Call("Register","SAÍDA","PAGAMENTO DE FORNECEDOR",40m);
        var after=Summary();Check(Value(after,"Final")==1260m&&Value(after,"Final")-Value(before,"Final")==60m,"Saída não subtraiu 40 / líquido não é 60");
        Check(Value(after,"ManualEntries")==100m&&Value(after,"ManualExits")==40m,"Totais manuais incorretos");
        using(var db=Database.Open())using(var q=db.CreateCommand()){q.CommandText="SELECT COUNT(*) FROM cash_movements WHERE sale_id IS NULL AND id IN($entry,$exit) AND occurred_at<>''";q.Parameters.AddWithValue("$entry",entry);q.Parameters.AddWithValue("$exit",exit);Check(Convert.ToInt32(q.ExecuteScalar())==2,"Persistência falhou");}
        var main=assembly.GetType("LealInfoPDV.MainForm",true)!;
        using(var owner=(Form)Activator.CreateInstance(main)!)
        {
            IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
            for(var round=0;round<2;round++)
            {
                Exception? error=null;
                using var timer=new System.Windows.Forms.Timer{Interval=100};
                timer.Tick+=(_,_)=>
                {
                    var f=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="FLUXO DE CAIXA");if(f==null)return;
                    try
                    {
                        var grid=Children(f).OfType<DataGridView>().Single();
                        Check(grid.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToInt64(r.Cells["ID"].Value)==entry)&&grid.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToInt64(r.Cells["ID"].Value)==exit),"Reabertura perdeu registro");
                        Check(Children(f).OfType<Button>().Any(b=>b.Text=="RESUMO DO CAIXA"),"Resumo ausente");
                        if(round==1){using var image=new System.Drawing.Bitmap(f.Width,f.Height);f.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,f.Width,f.Height));image.Save("fluxo-caixa-qa.png");}
                    }
                    catch(Exception ex){error=ex;}
                    timer.Stop();f.Close();
                };
                timer.Start();main.GetMethod("OpenFinance",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);if(error!=null)throw error;
            }
        }
        var reopened=Summary();Check(Value(reopened,"Final")==1260m,"Fechamento incorreto depois de reabrir");
        File.WriteAllText("cash-test-results.txt","PASS: Entrada +100; saída -40; efeito líquido +60. Saldo inicial 200 + recebimentos 1000 + entrada 100 - saída 40 = saldo final 1260. Tela fechada/reaberta duas vezes com registros persistidos. Valores inválidos bloqueados.");
        Console.WriteLine(File.ReadAllText("cash-test-results.txt"));
    }
}
