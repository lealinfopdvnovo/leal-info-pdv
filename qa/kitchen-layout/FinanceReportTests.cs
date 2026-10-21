using LealInfoPDV;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
internal static class FinanceReportTests
{
    static IEnumerable<Control> All(Control c)=>new[]{c}.Concat(c.Controls.Cast<Control>().SelectMany(All));
    internal static void Run(Action<bool,string> check)
    {
        // Reproduce the provider/DataTable type inference with the real report query.
        string source=File.ReadAllText("candidate/MainForm.cs");
        string sql=Regex.Match(source,@"SELECT m.id AS Código,[\s\S]*?ORDER BY m.occurred_at DESC, m.id DESC").Value;
        check(sql.Length>100,"financial regression uses the production movement query");
        using(var db=new SqliteConnection("Data Source=:memory:")){
            db.Open();using var seed=db.CreateCommand();seed.CommandText="CREATE TABLE cash_movements(id INTEGER,occurred_at TEXT,type TEXT,description TEXT,amount REAL,sale_id INTEGER); INSERT INTO cash_movements VALUES(1,'2026-01-01 10:00:00','ENTRADA','manual',10,NULL),(2,'2026-01-01 11:00:00','ENTRADA','venda',20,42);";seed.ExecuteNonQuery();
            DataTable Read(string query){using var q=db.CreateCommand();q.CommandText=query;q.Parameters.AddWithValue("$from","2026-01-01");q.Parameters.AddWithValue("$to","2026-01-02");q.Parameters.AddWithValue("$q","%%");q.Parameters.AddWithValue("$type","TODOS");using var r=q.ExecuteReader();var t=new DataTable();t.Load(r);return t;}
            bool reproduced=false;try{Read(sql.Replace("COALESCE(CAST(m.sale_id AS TEXT),'') AS Venda","COALESCE(m.sale_id,'') AS Venda"));}catch(FormatException){reproduced=true;}
            check(reproduced,"old report reproduces empty-string FormatException when a sale precedes manual cash movement");
            var fixedRows=Read(sql);check(fixedRows.Rows.Count==2&&fixedRows.Columns["Venda"]!.DataType==typeof(string)&&Convert.ToString(fixedRows.Rows[0]["Venda"])=="42"&&Convert.ToString(fixedRows.Rows[1]["Venda"])=="","fixed report preserves sale reference and blank manual reference without conversion");
            using var order=db.CreateCommand();order.CommandText="UPDATE cash_movements SET occurred_at='2026-01-01 12:00:00' WHERE id=1";order.ExecuteNonQuery();check(Read(sql).Rows.Count==2,"manual-first ordering also loads successfully");
            order.CommandText="DELETE FROM cash_movements";order.ExecuteNonQuery();check(Read(sql).Rows.Count==0,"empty financial period loads successfully");
        }
        using var cn=Database.Open();
        // Temporary fixture rows are removed by their exact generated IDs.
        long saleId=0;var movementIds=new List<long>();
        long Insert(string query){using var q=cn.CreateCommand();q.CommandText=query+"; SELECT last_insert_rowid();";return Convert.ToInt64(q.ExecuteScalar());}
        string day=DateTime.Today.ToString("yyyy-MM-dd");
        try{
            saleId=Insert($"INSERT INTO sales(sold_at,payment,subtotal,discount,total,operator) VALUES('{day} 11:00:00','PIX',20,0,20,'QA REPORT')");
            movementIds.Add(Insert($"INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES('{day} 10:00:00','ENTRADA','QA REPORT manual',10,NULL)"));
            movementIds.Add(Insert($"INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES('{day} 11:00:00','ENTRADA','QA REPORT venda',20,{saleId})"));
            Exception? failure=null;bool opened=false;using var main=new MainForm();using var timer=new System.Windows.Forms.Timer{Interval=150};
            timer.Tick+=(_,_)=>{
                var form=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text.Contains("RELATÓRIO FINANCEIRO"));if(form==null)return;timer.Stop();
                try{
                    var tabs=All(form).OfType<TabControl>().Single();var grid=tabs.TabPages[0].Controls.OfType<DataGridView>().Single();
                    check(grid.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToString(r.Cells["Venda"].Value)==saleId.ToString())&&grid.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToString(r.Cells["Descrição"].Value)=="QA REPORT manual"&&Convert.ToString(r.Cells["Venda"].Value)==""),"real financial window displays both paid sale and manual cash movement");
                    All(form).OfType<TextBox>().Single().Text="QA REPORT";All(form).OfType<Button>().Single(b=>b.Text=="BUSCAR").PerformClick();check(grid.Rows.Count==2,"financial search reload works with mixed movement references");
                    using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("layout-evidence/relatorio-financeiro-corrigido.png");opened=true;
                }catch(Exception e){failure=e;}finally{form.Close();}
            };
            timer.Start();typeof(MainForm).GetMethod("OpenReports",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,null);timer.Stop();if(failure!=null)throw failure;check(opened,"financial report opens and closes without unhandled conversion error");
        }finally{
            foreach(long id in movementIds){using var q=cn.CreateCommand();q.CommandText="DELETE FROM cash_movements WHERE id=$id";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();}
            if(saleId>0){using var q=cn.CreateCommand();q.CommandText="DELETE FROM sales WHERE id=$id";q.Parameters.AddWithValue("$id",saleId);q.ExecuteNonQuery();}
        }
    }
}
