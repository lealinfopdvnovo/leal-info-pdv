from pathlib import Path
import shutil,hashlib,json
root=Path('android-source/mobile/vidraceiro')
source=Path('controller/qa/etapa5b/android')
package=root/'app/src/main/java/br/com/lealinfo/vidraceiro'
old=(package/'SyncCoordinator.java').read_bytes()
shutil.copyfile(source/'SyncCoordinator.java',package/'SyncCoordinator.java')
shutil.copyfile(source/'QaIntegrationInstrumentation.java',root/'app/src/androidTest/java/br/com/lealinfo/vidraceiro/QaIntegrationInstrumentation.java')
p=root/'app/build.gradle';s=p.read_text();assert 'versionCode 18' in s
s=s.replace('versionCode 18','versionCode 19').replace("versionName '0.3.6'","versionName '0.3.7-qa005'").replace('applicationIdSuffix ".profissional022"','applicationIdSuffix ".angraqa005"').replace("testInstrumentationRunner 'br.com.lealinfo.vidraceiro.MeasureFlowInstrumentation'","testInstrumentationRunner 'br.com.lealinfo.vidraceiro.QaIntegrationInstrumentation'");p.write_text(s)
Path('integration-evidence').mkdir(exist_ok=True)
Path('integration-evidence/android-overlay.json').write_text(json.dumps(dict(SourceCommit='acb72ed',OriginalSyncSHA256=hashlib.sha256(old).hexdigest(),CandidateSyncSHA256=hashlib.sha256((package/'SyncCoordinator.java').read_bytes()).hexdigest(),ApplicationId='br.com.lealinfo.vidraceiro.angraqa005',ProductionApplicationIdUnchanged=True,NoEndpointChange=True,Published=False),indent=2))
