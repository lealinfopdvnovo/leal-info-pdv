"""Candidate-only compatibility gate; never reads or modifies production feeds."""
from pathlib import Path
root=Path('candidate')
p=root/'PetRuntime.cs'
s=p.read_text(encoding='utf-8-sig')
marker='        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));'
assert s.count(marker)==1, 'Unknown PET loader; refuse patch'
gate='''        using(var compatibility=JsonDocument.Parse(File.ReadAllText(path)))
        {
            var document=compatibility.RootElement;
            string? Bound(string name)=>document.TryGetProperty(name,out var value)?value.GetString():null;
            LealInfo.Components.IndependentUpdater.ValidateCompatibility(UpdateManager.CurrentVersion,Bound("MinGlobalVersion"),Bound("MaxGlobalVersionExclusive"));
        }
'''
p.write_text(s.replace(marker,gate+marker),encoding='utf-8')
print('PASS compatibility checked before loading any external PET code')
