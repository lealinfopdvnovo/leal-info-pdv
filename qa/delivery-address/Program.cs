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
        Console.WriteLine("PASS: consulta real ViaCEP, endereço completo persistido, código preservado e referência separada do destino.");
    }
}
