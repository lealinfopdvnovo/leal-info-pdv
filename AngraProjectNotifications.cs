using System.Text;
using System.Text.Json;
namespace LealInfoPDV;

internal sealed record AngraProjectNotice(string Id,string LocalId,string Title,string Customer,bool Viewed);
// Sidecar receipt journal: never changes projects, customers, financial data or the operational schema.
internal sealed class AngraProjectNotifications
{
 private readonly object gate=new();private readonly string path;private readonly HashSet<string> known=new();private readonly List<AngraProjectNotice> notices=new();
 internal AngraProjectNotifications(AngraSyncStore store){path=Path.Combine(Database.AppFolder,"angra-notices-"+AngraSyncService.Hash(Encoding.UTF8.GetBytes(store.Tenant))+".json");if(File.Exists(path)){var saved=JsonSerializer.Deserialize<Journal>(File.ReadAllText(path))??throw new IOException("Avisos do app indisponíveis; arquivo preservado.");if(saved.Tenant!=store.Tenant)throw new IOException("Avisos de outro vínculo recusados.");known.UnionWith(saved.Known);notices.AddRange(saved.Notices);}else{known.UnionWith(store.Links().Where(l=>l.Entity=="projects"&&l.LocalId!=null).Select(l=>l.Id));Save(store.Tenant);}Tenant=store.Tenant;}
 private string Tenant{get;}
 private sealed record Journal(string Tenant,string[] Known,AngraProjectNotice[] Notices);
 private void Save(string tenant){Directory.CreateDirectory(Database.AppFolder);string tmp=path+".tmp";using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){var bytes=JsonSerializer.SerializeToUtf8Bytes(new Journal(tenant,known.ToArray(),notices.ToArray()));f.Write(bytes);f.Flush(true);}File.Move(tmp,path,true);}
 internal IReadOnlyList<AngraProjectNotice> Unread(){lock(gate)return notices.Where(n=>!n.Viewed).Reverse().ToArray();}
 internal void TrackNative(AngraSyncStore store){lock(gate){bool changed=false;foreach(var l in store.Links().Where(l=>l.Entity=="projects"&&l.LocalId!=null&&l.Revision==0))changed|=known.Add(l.Id);if(changed)Save(Tenant);}}
 internal void Observe(AngraSyncStore store){lock(gate){bool changed=false;foreach(var l in store.Links().Where(l=>l.Entity=="projects"&&l.LocalId!=null&&l.Revision>0&&l.Conflict==null&&!known.Contains(l.Id))){var p=l.Baseline;if(p==null)continue;/* A receipt still queued for the server must not hide an already imported project. Local edits remain pending for review. */if(l.Pending!=null&&!System.Text.Json.Nodes.JsonNode.DeepEquals(l.Pending["payload"],p))continue;if(p["photoAttachment"] is System.Text.Json.Nodes.JsonObject photo){string sha=AngraProjectData.Text(photo["sha256"]);string file=AngraSyncService.PhotoPath(l.Id,sha);if(!File.Exists(file)||AngraSyncService.Hash(File.ReadAllBytes(file))!=sha)continue;}string customerId=AngraProjectData.Text(p["clientLocalId"]);string customer=customerId==""?"Cliente não informado":AngraProjectData.Text(store.ReadLocal(store.Link("clients",customerId))?["name"]);notices.Add(new(l.Id,l.LocalId!,AngraProjectData.Text(p["title"]),customer,false));known.Add(l.Id);changed=true;}if(changed)Save(Tenant);}}
 internal void MarkViewed(string localId){lock(gate){bool changed=false;for(int i=0;i<notices.Count;i++)if(notices[i].LocalId==localId&&!notices[i].Viewed){notices[i]=notices[i] with{Viewed=true};changed=true;}if(changed)Save(Tenant);}}
}
