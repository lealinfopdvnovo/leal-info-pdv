using System.Text.Json;
using System.Text.RegularExpressions;
namespace PetOrganization;
public sealed record PetProfile(string InternalId,string DisplayName,string PetVersion);
public sealed record PetCatalog(int SchemaVersion,string Status,string GlobalReferenceVersion,string GlobalReferenceCommit,PetProfile[] Profiles)
{
 public static PetCatalog Load()
 {
  using var resource=typeof(PetCatalog).Assembly.GetManifestResourceStream("PetOrganization")!;
  var catalog=JsonSerializer.Deserialize<PetCatalog>(resource)??throw new InvalidDataException("Catálogo ausente.");catalog.Validate();return catalog;
 }
 public void Validate()
 {
  if(SchemaVersion!=1||Status!="PREPARED_NOT_PUBLISHED"||!Version.TryParse(GlobalReferenceVersion,out _)||!Regex.IsMatch(GlobalReferenceCommit??"",@"\A[0-9a-f]{40}\z")||Profiles==null||Profiles.Length==0)throw new InvalidDataException("Organização incompatível.");
  var ids=new HashSet<string>(StringComparer.Ordinal);var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var p in Profiles)
   if(!Regex.IsMatch(p.InternalId??"",@"\A\d{3}\z")||!ids.Add(p.InternalId)||string.IsNullOrWhiteSpace(p.DisplayName)||p.DisplayName!=p.DisplayName.Trim()||!names.Add(p.DisplayName)||!Regex.IsMatch(p.PetVersion??"",@"\A1\.\d+(?:\.\d+)*\z"))throw new InvalidDataException("Nome, identificador ou versão PET inválidos.");
 }
 public PetProfile For(string internalId)=>Profiles.Single(p=>p.InternalId==internalId);
 public PetCatalog Advance(string internalId,string petVersion)
 {
  var old=For(internalId);
  if(!Regex.IsMatch(petVersion??"",@"\A1\.\d+(?:\.\d+)*\z")||Version.Parse(petVersion)<=Version.Parse(old.PetVersion))throw new InvalidDataException("A versão PET deve avançar independentemente.");
  // Draft in memory only. No database, activation, settings or updater feed is written.
  var next=this with{Profiles=Profiles.Select(p=>p.InternalId==internalId?p with{PetVersion=petVersion}:p).ToArray()};next.Validate();return next;
 }
}
