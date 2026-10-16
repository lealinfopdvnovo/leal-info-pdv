"""Negative publication tests: invalid candidates must not change any update feed."""
import importlib.util,json,os,tempfile,unittest,zipfile,hashlib
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('publisher',ROOT/'scripts/distribute_channels.py')
p=importlib.util.module_from_spec(spec);spec.loader.exec_module(p)
class PublicationGuards(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.old=Path.cwd();os.chdir(self.temp.name)
  self.entries=[];self.before={};self.releases=[]
  for ch in p.CHANNELS:
   old={'Version':'10.406','SourceCommit':'a'*40};self.before[ch]={'sha':ch+'old','manifest':old}
   e={'Channel':ch,'Version':'10.407','SourceCommit':'b'*40,'FeedPath':p.feedpath(ch),'Tag':ch+'407','Package':ch+'.zip','Url':'https://example.invalid/'+ch+'.zip'}
   folder=Path('artifacts')/('candidate-'+ch);folder.mkdir(parents=True)
   files=p.FILES if ch!='GLOBAL' else {'LealInfoPDV.exe','LealInfoPDV.ChannelLauncher.dll','LealInfoPDV.ChannelLauncher.deps.json','LealInfoPDV.ChannelLauncher.runtimeconfig.json',*(f'LealInfoPDV.Core.{c}.dll' for c in p.CHANNELS)}
   with zipfile.ZipFile(folder/e['Package'],'w') as z:
    for name in files:z.writestr(name,('test '+name).encode())
   target={name:hashlib.sha256(('test '+name).encode()).hexdigest() for name in files}
   proof={**e,'PackageSha256':p.digest(folder/e['Package']),'TargetFiles':target,'BuildRunId':'123','TestsPassed':True,'PayloadPolicyPassed':True,'LegacySafeGlobal':ch=='GLOBAL','InstalledCoreChannels':{}}
   p.write(folder/'candidate.json',proof);self.entries.append(e)
  self.plan={'RunId':'123','ControllerCommit':'c'*40,'Before':self.before,'Entries':self.entries}
  p.write('release-plan.json',self.plan)
 def tearDown(self):os.chdir(self.old);self.temp.cleanup()
 def execute(self):
  def command(*args,**kwargs):
   if args[:3]==('git','rev-parse','HEAD'):return 'c'*40+'\n'
   self.releases.append(args);raise AssertionError('External mutation reached before candidate guards')
  with patch.dict(os.environ,{'GITHUB_RUN_ID':'123','GITHUB_SHA':'c'*40}),patch.object(p,'readfeed',side_effect=lambda ch:self.before[ch]),patch.object(p,'run',side_effect=command):p.publish()
 def test_global_replacing_business_dll_is_blocked(self):
  f=Path('artifacts/candidate-GLOBAL/candidate.json');o=json.loads(f.read_text());o['LegacySafeGlobal']=False;p.write(f,o)
  with self.assertRaisesRegex(RuntimeError,'GLOBAL must preserve'):self.execute()
  self.assertFalse(self.releases)
 def test_failed_windows_blocks_all_publication(self):
  f=Path('artifacts/candidate-002/candidate.json');o=json.loads(f.read_text());o['TestsPassed']=False;p.write(f,o)
  with self.assertRaisesRegex(RuntimeError,'Windows checks failed'):self.execute()
  self.assertFalse(self.releases)
 def test_changed_package_blocks_all_publication(self):
  with open('artifacts/candidate-003/003.zip','ab') as f:f.write(b'tampered')
  with self.assertRaisesRegex(RuntimeError,'package bytes changed'):self.execute()
  self.assertFalse(self.releases)
 def test_global_without_legacy_client_versions_is_blocked(self):
  self.plan['Entries']=[self.entries[0]];p.write('release-plan.json',self.plan)
  with self.assertRaisesRegex(RuntimeError,'Legacy version fence failed'):self.execute()
  self.assertFalse(self.releases)
 def test_concurrent_feed_change_blocks_publication(self):
  self.before['002']={'sha':'newer-owner-change','manifest':self.before['002']['manifest']}
  with self.assertRaisesRegex(RuntimeError,'channel changed during build'):self.execute()
  self.assertFalse(self.releases)
 def test_cross_channel_artifact_rejected(self):
  f=Path('artifacts/candidate-002/candidate.json');o=json.loads(f.read_text());o['SourceCommit']='d'*40;p.write(f,o)
  with self.assertRaisesRegex(RuntimeError,'candidate identity differs'):self.execute()
  self.assertFalse(self.releases)
if __name__=='__main__':unittest.main()
