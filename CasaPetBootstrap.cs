using System.Security.Cryptography;
using System.Text.Json;
namespace LealInfoPDV;
internal static class CasaPetBootstrap
{
 internal static void Initialize()
 {
  string id=Licensing.InstallationLicense.Current?.ClientCode??"";
  if(!ClientUpdateIdentity.IsValidClientCode(id)){
   using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT value FROM settings WHERE key='client_update_id'";id=Convert.ToString(q.ExecuteScalar())??"";
  }
  if(id!="003")return;
  using var resource=typeof(CasaPetBootstrap).Assembly.GetManifestResourceStream("Casa.PET.1.1");
  if(resource==null)throw new InvalidDataException("Personalização CASA V1.1 ausente.");
  using var bytes=new MemoryStream();resource.CopyTo(bytes);var content=bytes.ToArray();string hash=Convert.ToHexString(SHA256.HashData(content));
  string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LEAL INFO PDV","PET","003","1.1",hash);
  Directory.CreateDirectory(folder);string dll=Path.Combine(folder,"Casa.dll");
  // Content-addressed extraction: all bytes originate in the protected, backed-up business DLL.
  // Rolling back that DLL also rolls back the embedded PET. Existing customer data is never touched.
  var temp=dll+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllBytes(temp,content);File.Move(temp,dll,true);
  var manifest=JsonSerializer.Serialize(new{Identity="003",Version="1.1",ApiVersion=1,Assembly="Casa.dll",Sha256=hash});
  temp=Path.Combine(folder,Guid.NewGuid().ToString("N")+".tmp");File.WriteAllText(temp,manifest);File.Move(temp,Path.Combine(folder,"manifest.json"),true);
  PetRuntime.Load(folder,id);
 }
}

