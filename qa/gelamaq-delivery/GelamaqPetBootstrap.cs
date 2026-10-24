namespace LealInfoPDV;
internal static class GelamaqPetBootstrap
{
 internal static void Initialize()
 {
  var identity=Licensing.InstallationLicense.Current?.ClientCode??"";
  if(!ClientUpdateIdentity.IsValidClientCode(identity))
  {
   using var cn=Database.Open();using var cmd=cn.CreateCommand();
   cmd.CommandText="SELECT value FROM settings WHERE key='client_update_id'";
   identity=Convert.ToString(cmd.ExecuteScalar())??"";
  }
  Initialize(identity);
 }
 internal static void Initialize(string identity)
 {
  // Only this independently built 002 host contains this bridge and PET resource.
  if(identity=="002")PetRuntime.LoadEmbedded(identity);
 }
}
