using System.Reflection;
using LealInfoPDV;
using PetOrganization;
public sealed class DisplayExtension:IPetExtension
{
 public void Customize(Form form)
 {
  if(form.GetType().Name!="MainForm")return;
  var identity=typeof(DisplayExtension).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="PetIdentity").Value!;
  var profile=PetCatalog.Load().For(identity);
  string marker=" — "+profile.DisplayName+" • PET V"+profile.PetVersion;
  if(!form.Text.EndsWith(marker,StringComparison.Ordinal))form.Text+=marker;
 }
}
