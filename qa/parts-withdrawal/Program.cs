using System.Reflection;
using LealInfoPDV;
internal static class Program
{
    const BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        Application.ThreadException+=(_,e)=>{Console.Error.WriteLine(e.Exception.ToString());Environment.Exit(1);};
        Database.Initialize();Application.EnableVisualStyles();
        _=Task.Run(async()=>{await Task.Delay(TimeSpan.FromSeconds(100));Environment.FailFast("Timeout da automação de retirada");});
        void Check(bool v,string reason){if(!v)throw new Exception(reason);}
        long Count(string table){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT COUNT(*) FROM "+table;return Convert.ToInt64(q.ExecuteScalar());}
        double Stock(){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT COALESCE(SUM(stock),0) FROM products";return Convert.ToDouble(q.ExecuteScalar());}
        var assembly=typeof(Database).Assembly;var store=assembly.GetType("LealInfoPDV.PartsWithdrawal",true)!;
        if(args.Contains("--verify-restart"))
        {
            Check(Count("parts_withdrawals")==2,"Reinício perdeu histórico");
            var before=File.ReadAllText("parts-side-effects.txt").Split('|');
            Check(Count("cash_movements").ToString()==before[0]&&Count("sales").ToString()==before[1]&&Stock().ToString(System.Globalization.CultureInfo.InvariantCulture)==before[2],"Estoque/financeiro alterados");
            Console.WriteLine("PASS: novo processo do PDV manteve 2 registros; estoque, vendas e financeiro intactos.");return;
        }
        File.WriteAllText("parts-side-effects.txt",$"{Count("cash_movements")}|{Count("sales")}|{Stock().ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        long customer;
        using(var db=Database.Open())using(var q=db.CreateCommand()){q.CommandText="INSERT INTO customers(name,document,phone) VALUES('QA EMPRESA ABC','12345678000199','24999999999'); SELECT last_insert_rowid();";customer=Convert.ToInt64(q.ExecuteScalar());}
        foreach(var term in new[]{"QA EMPRESA ABC","12345678000199","24999999999",customer.ToString()})
        {var data=(System.Data.DataTable)store.GetMethod("FindCustomers",Private)!.Invoke(null,new object[]{term})!;Check(data.Rows.Cast<System.Data.DataRow>().Any(r=>Convert.ToInt64(r["ID"])==customer),"Pesquisa falhou: "+term);}
        var main=assembly.GetType("LealInfoPDV.MainForm",true)!;
        using var owner=(Form)Activator.CreateInstance(main)!;
        IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
        Button Button(Form f,string name)=>Children(f).OfType<Button>().Single(b=>b.Name==name);
        TextBox Box(Form f,string name)=>Children(f).OfType<TextBox>().Single(b=>b.Name==name);
        for(var round=0;round<3;round++)
        {
            var phase=0;var ticks=0;Exception? failure=null;var started=DateTime.UtcNow;
            using var timer=new System.Windows.Forms.Timer{Interval=100};
            timer.Tick+=(_,_)=>
            {
                var forms=Application.OpenForms.Cast<Form>().ToArray();
                if(++ticks%50==0)Console.WriteLine("QA WAIT phase="+phase+" forms="+string.Join(";",forms.Select(f=>f.Text+" visible="+f.Visible+" enabled="+f.Enabled))+" records="+Count("parts_withdrawals"));
                var history=forms.FirstOrDefault(f=>f.Text=="RETIRADA DE PEÇAS");if(history==null)return;
                try
                {
                    if(DateTime.UtcNow-started>TimeSpan.FromSeconds(30))throw new Exception("Timeout da tela");
                    var entry=forms.FirstOrDefault(f=>f.Text=="NOVA RETIRADA");var search=forms.FirstOrDefault(f=>f.Text=="BUSCAR CLIENTE CADASTRADO");var confirm=forms.FirstOrDefault(f=>f.Text=="CONFIRMAR RETIRADA?");
                    if(phase==0)
                    {
                        if(round==2){Check(Count("parts_withdrawals")==2,"Reabertura perdeu registros");timer.Stop();history.Close();return;}
                        phase=1;Console.WriteLine("QA: abrir Nova Retirada");history.BeginInvoke(new Action(()=>Button(history,"newWithdrawal").PerformClick()));return;
                    }
                    if(phase==1&&entry!=null)
                    {
                        Box(entry,"description").Text=round==0?"Fonte ATX 500W":"Notebook Dell Inspiron";Box(entry,"collectedBy").Text=round==0?"MARIA AVULSA":"JOÃO DA SILVA";
                        if(round==1){phase=2;Console.WriteLine("QA: executar F8");entry.BeginInvoke(new Action(()=>typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(entry,new object[]{new KeyEventArgs(Keys.F8)})));return;}
                        phase=4;Console.WriteLine("QA: salvar e aguardar confirmação");entry.BeginInvoke(new Action(()=>Button(entry,"saveWithdrawal").PerformClick()));return;
                    }
                    if(phase==2&&search!=null)
                    {
                        Box(search,"customerSearch").Text="QA EMPRESA ABC";var grid=Children(search).OfType<DataGridView>().Single();
                        Check(Convert.ToInt64(grid.CurrentRow!.Cells["ID"].Value)==customer,"Cliente selecionado incorreto");phase=3;Console.WriteLine("QA: selecionar cliente real");search.BeginInvoke(new Action(()=>Button(search,"selectCustomer").PerformClick()));return;
                    }
                    if(phase==3&&entry!=null){Check(Box(entry,"customer").Text=="QA EMPRESA ABC","Nome vinculado errado");phase=4;Console.WriteLine("QA: salvar e aguardar confirmação");entry.BeginInvoke(new Action(()=>Button(entry,"saveWithdrawal").PerformClick()));return;}
                    if(phase==4&&confirm!=null)
                    {
                        Check(Count("parts_withdrawals")==round,"Gravou antes de confirmar");phase=5;Console.WriteLine("QA: confirmar retirada");confirm.BeginInvoke(new Action(()=>{var button=Button(confirm,"confirmWithdrawal");Console.WriteLine("QA CONFIRM enabled="+button.Enabled+" visible="+button.Visible+" canSelect="+button.CanSelect);button.PerformClick();Console.WriteLine("QA CONFIRM click finished");}));return;
                    }
                    if(phase==5&&entry==null&&confirm==null)
                    {
                        var grid=Children(history).OfType<DataGridView>().Single();Check(grid.Rows.Count==round+1,"Histórico não atualizou");
                        if(round==1){using var image=new System.Drawing.Bitmap(history.Width,history.Height);history.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,history.Width,history.Height));image.Save("retirada-pecas-qa.png");}
                        timer.Stop();history.Close();
                    }
                }
                catch(Exception ex){failure=ex;timer.Stop();foreach(var f in forms.Reverse())if(f!=owner)f.Close();}
            };
            timer.Start();main.GetMethod("OpenPartsWithdrawals",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);if(failure!=null)throw failure;
        }
        using(var db=Database.Open())using(var q=db.CreateCommand())
        {
            q.CommandText="SELECT customer_id,customer_name,collected_by FROM parts_withdrawals ORDER BY id";using var rd=q.ExecuteReader();
            Check(rd.Read()&&rd.IsDBNull(0)&&rd.GetString(2)=="MARIA AVULSA","Retirada avulsa incorreta");
            Check(rd.Read()&&rd.GetInt64(0)==customer&&rd.GetString(1)=="QA EMPRESA ABC"&&rd.GetString(2)=="JOÃO DA SILVA","Vínculo ID/pessoa incorreto");
        }
        Console.WriteLine("PASS: módulo aberto; 2 retiradas confirmadas na interface; avulsa e cliente por ID; pessoa diferente; pesquisa por nome/documento/telefone/ID; F8 executado; histórico atualizado e persistido após reabrir.");
    }
}
