using System.Security.Cryptography;
using System.Text.Json;
namespace LealInfoVendedor;
internal sealed record Customer(string Code,string Name,string Document,string Serial,string Plan,int Computers,bool Active)
{
    public override string ToString()=>Code+" • "+Name;
}
internal static class CustomerLedger
{
    internal static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LealInfoVendedor","clientes.protected");
    internal static List<Customer> Read()=>File.Exists(PathName)?JsonSerializer.Deserialize<List<Customer>>(ProtectedData.Unprotect(File.ReadAllBytes(PathName),null,DataProtectionScope.CurrentUser))??new():new();
    internal static string NextCode()=>(Read().Select(c=>int.Parse(c.Code)).DefaultIfEmpty(0).Max()+1).ToString("D3");
    internal static void Save(Customer customer)
    {
        var all=Read();all.RemoveAll(c=>c.Code==customer.Code);all.Add(customer);
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);var temp=PathName+".tmp";
        File.WriteAllBytes(temp,ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(all),null,DataProtectionScope.CurrentUser));File.Move(temp,PathName,true);
    }
}
