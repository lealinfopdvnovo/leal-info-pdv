using System.Reflection;
using LealInfoPDV;
internal static class Program
{
    [STAThread] private static void Main()
    {
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var type=typeof(Database).Assembly.GetType("LealInfoPDV.DeliveryAddress",true)!;
        object Call(string name,params object[] args)=>type.GetMethod(name,flags)!.Invoke(null,args)!;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        Check((string)Call("NormalizeCep","23900-000")=="23900000","Máscara CEP");
        Check((string)Call("NormalizeCep","23900X000")=="","CEP inválido aceito");
        var task=(Task)Call("LookupAsync","01001-000",CancellationToken.None);task.GetAwaiter().GetResult();
        var result=task.GetType().GetProperty("Result")!.GetValue(task)!;
        string Read(string property)=>(string)result.GetType().GetProperty(property)!.GetValue(result)!;
        Check(Read("Street")=="Praça da Sé"&&Read("District")=="Sé"&&Read("City")=="São Paulo"&&Read("Uf")=="SP","Consulta real ViaCEP");
        try{Call("Parse","{\"erro\":true}");throw new Exception("CEP inexistente aceito");}catch(TargetInvocationException e) when(e.InnerException is ArgumentException){}
        var address=(string)Call("Compose","23900-000","Rua do Comércio","125","Centro","Angra dos Reis","rj");
        Check(address=="Rua do Comércio, 125, Centro, Angra dos Reis - RJ, CEP 23900-000","Endereço incompleto");
        var additional=(string)Call("Additional","Casa 2","Perto da igreja");
        Check(!address.Contains("igreja")&&!address.Contains("Casa 2")&&additional.Contains("Casa 2")&&additional.Contains("igreja"),"Referência contaminou destino");
        Database.Initialize();
        var deliveries=typeof(Database).Assembly.GetType("LealInfoPDV.DeliveryManagementForm",true)!;
        deliveries.GetMethod("EnsureTrackingSchema",flags)!.Invoke(null,null);
        var id=(long)deliveries.GetMethod("InsertDeliveryWithTracking",flags)!.Invoke(null,new object[]{DateTime.Now.ToString("s"),"QA CEP","",address,additional,"TESTE",0d,0d,"TESTE",DBNull.Value,"QA","QA"})!;
        using var db=Database.Open();using var query=db.CreateCommand();query.CommandText="SELECT d.address,d.reference,t.code FROM deliveries d JOIN delivery_tracking t ON t.delivery_id=d.id WHERE d.id=$id";query.Parameters.AddWithValue("$id",id);
        using(var rd=query.ExecuteReader()){Check(rd.Read()&&rd.GetString(0)==address&&rd.GetString(1)==additional&&rd.GetString(2).Length==12,"Persistência/código alterados");}
        Application.EnableVisualStyles();
        using(var central=(Form)Activator.CreateInstance(deliveries)!)
        using(var timer=new System.Windows.Forms.Timer{Interval=100})
        {
            Exception? failure=null;var phase=0;var started=DateTime.UtcNow;
            IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
            TextBox Field(Form form,string label)
            {
                var title=Children(form).OfType<Label>().Single(l=>l.Text==label);
                return title.Parent!.Controls.OfType<TextBox>().Single();
            }
            timer.Tick+=(_,_)=>
            {
                var form=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="Nova Entrega");
                if(form==null)return;
                try
                {
                    if(DateTime.UtcNow-started>TimeSpan.FromSeconds(30))throw new Exception("Timeout no preenchimento automático da tela");
                    if(phase==0){Field(form,"CEP").Text="01001-000";phase=1;return;}
                    if(Field(form,"Cidade").Text!="São Paulo")return;
                    foreach(var label in new[]{"CEP","Rua/Logradouro","Número","Complemento","Bairro","Cidade","UF","Referência"})
                        Check(!Field(form,label).ReadOnly&&Field(form,label).Enabled,"Campo não editável: "+label);
                    Check(Field(form,"Rua/Logradouro").Text=="Praça da Sé"&&Field(form,"Bairro").Text=="Sé"&&Field(form,"UF").Text=="SP","Preenchimento da tela");
                    Field(form,"Número").Text="125";Field(form,"Complemento").Text="Casa 2";Field(form,"Referência").Text="Perto da igreja";
                    using(var image=new System.Drawing.Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,form.Width,form.Height));image.Save("nova-entrega-cep.png");}
                    timer.Stop();form.Close();
                }
                catch(Exception ex){failure=ex;timer.Stop();form.Close();}
            };
            timer.Start();deliveries.GetMethod("EditDelivery",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(central,new object?[]{null});
            if(failure!=null)throw failure;
        }
        Console.WriteLine("PASS: consulta real ViaCEP, endereço completo persistido, código preservado e referência separada do destino.");
    }
}
