import hashlib,io,json,os,re,shutil,urllib.request,zipfile
from pathlib import Path
assert os.environ['CHANNEL']=='002'
r=Path('candidate'); c=Path('controller')
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def get(url):
 with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'LEAL-PET-GELAMAQ'}),timeout=90) as f:return f.read()
feed=json.loads(get('https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/002/version.json'))
assert feed['Version']=='10.457' and feed['Sha256']=='f03f15bcb412086a5340fcf9f5c968c12fc6f82b93f282850965086bc4bd56f4'
data=get(feed['PackageUrl']);assert hashlib.sha256(data).hexdigest()==feed['Sha256']
Path('published002').mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(data)) as z:
 assert set(z.namelist())=={'LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json'}
 for name in z.namelist():Path('published002',name).write_bytes(z.read(name))
Path('baseline-002.json').write_text(json.dumps(feed,indent=2))
before={str(p.relative_to(r)):digest(p) for p in r.rglob('*') if p.is_file() and not any(x in p.parts for x in ('.git','obj','bin'))}
assert 'CurrentVersion="10.457"' in (r/'UpdateManager.cs').read_text()
assert "'002'" in (r/'TransactionalUpdate.ps1').read_text()
# Reuse the approved d9181db extension unchanged. No other client source is rebuilt.
for name in ('PetRuntime.cs','IFinancialMovementsPet.cs'):(r/name).write_bytes((c/name).read_bytes())
for name in ('FinanceExtension.cs','GelamaqPet.csproj'):
 p=r/'qa/gelamaq-pet'/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes((c/'qa/gelamaq-pet'/name).read_bytes())
for name in ('GelamaqPetBootstrap.cs',): (r/name).write_bytes((c/'qa/gelamaq-delivery'/name).read_bytes())
p=r/'PetRuntime.cs';s=p.read_text();marker='    internal static void Load(string folder,string identity)'
assert marker in s
s=s.replace(marker,'''    internal static void LoadEmbedded(string identity)
    {
        if(identity!="002")return;
        if(extension!=null)throw new InvalidOperationException("PET já carregado.");
        using var manifest=typeof(PetRuntime).Assembly.GetManifestResourceStream("Gelamaq.PET.manifest") ?? throw new InvalidDataException("Manifesto PET Gelamaq ausente.");
        var m=JsonSerializer.Deserialize<Manifest>(manifest) ?? throw new InvalidDataException("Manifesto PET vazio.");
        if(m.Identity!="002" || m.Version!="1.1" || m.ApiVersion!=ApiVersion || m.Assembly!="LealInfoPDV.Pet.002.dll")throw new InvalidDataException("PET Gelamaq incompatível.");
        using var payload=typeof(PetRuntime).Assembly.GetManifestResourceStream("Gelamaq.PET.payload") ?? throw new InvalidDataException("PET Gelamaq ausente.");
        using var bytes=new MemoryStream();payload.CopyTo(bytes);
        if(!Convert.ToHexString(SHA256.HashData(bytes.ToArray())).Equals(m.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Integridade PET Gelamaq inválida.");
        bytes.Position=0;
        var assembly=AssemblyLoadContext.Default.LoadFromStream(bytes);
        var types=assembly.GetExportedTypes().Where(t=>!t.IsAbstract&&typeof(IPetExtension).IsAssignableFrom(t)).ToArray();
        if(types.Length!=1)throw new InvalidDataException("Contrato PET Gelamaq inválido.");
        extension=(IPetExtension)Activator.CreateInstance(types[0])!;
        if(extension is not IFinancialMovementsPet)throw new InvalidDataException("Extensão financeira PET ausente.");
        loadedIdentity=identity;CurrentVersion=m.Version;
        Application.Idle+=Apply;
    }
''' + marker,1);p.write_text(s)
p=r/'MainForm.cs';s=p.read_text();a=s.index('        DataTable QueryTable(');b=s.index('        double QueryValue(',a);part=s[a:b]
assert part.count('using var cmd = cn.CreateCommand();')==1
part=part.replace('            using var cmd = cn.CreateCommand();','            var petReport = includeMovementType ? PetRuntime.FinancialMovements : null;\n            if (petReport != null) sql = petReport.PrepareSql(sql);\n            using var cmd = cn.CreateCommand();',1)
part=part.replace('            var table = new DataTable(); table.Load(reader); return table;','            if (petReport != null) return petReport.ReadTable(reader);\n            var table = new DataTable(); table.Load(reader); return table;',1)
s=s[:a]+part+s[b:];p.write_text(s)
p=r/'Program.cs';s=p.read_text();assert '            Database.Initialize();' in s;s=s.replace('            Database.Initialize();','            Database.Initialize();\n            GelamaqPetBootstrap.Initialize();',1);p.write_text(s)
# Legacy feeds and native notifier require a monotonically increasing 10.N delivery number.
# This is the 002 transport revision only: GLOBAL feature/assembly version remains 10.457.
p=r/'UpdateManager.cs';s=p.read_text();s=s.replace('public const string CurrentVersion="10.457";','public const string CurrentVersion="10.457";\n internal const string InstalledUpdateVersion="10.458";',1)
assert 'LoadManifestAsync(http,GetClientCode(),CurrentVersion)' in s
s=s.replace('LoadManifestAsync(http,GetClientCode(),CurrentVersion)','LoadManifestAsync(http,GetClientCode(),InstalledUpdateVersion)',1);p.write_text(s)
p=r/'TransactionalUpdate.cs';s=p.read_text();assert 'Version=UpdateManager.CurrentVersion' in s;s=s.replace('Version=UpdateManager.CurrentVersion','Version=UpdateManager.InstalledUpdateVersion',1);p.write_text(s)
p=r/'LealInfoPDV.csproj';s=p.read_text();assert '<FileVersion>10.457.0.0</FileVersion>' in s;s=s.replace('<FileVersion>10.457.0.0</FileVersion>','<FileVersion>10.458.0.0</FileVersion>')
s=s.replace('</Project>', '''  <ItemGroup Condition="'$(GelamaqPetPayload)' != ''"><EmbeddedResource Include="$(GelamaqPetPayload)" LogicalName="Gelamaq.PET.payload" /><EmbeddedResource Include="$(GelamaqPetManifest)" LogicalName="Gelamaq.PET.manifest" /></ItemGroup>
</Project>''');p.write_text(s)
changed=[name for name,h in before.items() if digest(r/name)!=h]
assert set(changed)=={'MainForm.cs','Program.cs','UpdateManager.cs','TransactionalUpdate.cs','LealInfoPDV.csproj'},changed
assert digest(r/'TransactionalUpdate.ps1')==before['TransactionalUpdate.ps1']
protected={n:h for n,h in before.items() if n not in changed}
Path('gelamaq-scope.json').write_text(json.dumps(dict(Client='002',GlobalVersion='10.457',PetVersion='1.1',DeliveryRevision='10.458',ChangedExistingSource=changed,ProtectedSourceHashes=protected,Published=False,InstalledOnCustomer=False,DeliveryFormat='existing protected three-file package; independently compiled PET embedded and rolled back with host'),indent=2))
for name in ('Program.cs','Tests.csproj','rollback.ps1'):
 p=r/'qa/gelamaq-delivery'/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes((c/'qa/gelamaq-delivery'/name).read_bytes())
print('PASS actual Gelamaq 10.457 base preserved; minimal 002 hook and protected PET delivery prepared')
