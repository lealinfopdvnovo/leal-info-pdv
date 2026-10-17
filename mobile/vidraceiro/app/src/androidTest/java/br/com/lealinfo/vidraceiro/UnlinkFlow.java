package br.com.lealinfo.vidraceiro;
import org.json.*;import java.io.*;import java.nio.file.Files;import java.nio.charset.StandardCharsets;
/** Native UI plus actual gateway; PDV keys only issue codes, no running PDV process. */
final class UnlinkFlow {
 static void waitDone(MeasureFlowInstrumentation t)throws Exception{long end=System.currentTimeMillis()+90000;while(System.currentTimeMillis()<end){Thread.sleep(200);t.waitForIdleSync();if(!t.a.sync.busy)return;}throw new Exception("timeout");}
 static void dialog(MeasureFlowInstrumentation t,String label)throws Exception{Thread.sleep(350);t.waitForIdleSync();t.check(t.select(t.getUiAutomation().getRootInActiveWindow(),label),"confirmation button "+label);t.waitForIdleSync();}
 static void pair(MeasureFlowInstrumentation t,String code)throws Exception{t.ui(()->t.a.integration());t.set("Código de conexão mostrado no PDV",code);t.click("CONECTAR AO PDV");waitDone(t);t.check(t.a.sync.state.optString("error").isEmpty(),t.a.sync.status());}
 static void deny()throws Exception{try{SyncTransport.request("/sync/exchange",new JSONObject().put("changes",new JSONArray()));throw new AssertionError("unlinked handset retained access");}catch(IOException expected){}}
 static void run(MeasureFlowInstrumentation t,String phase)throws Exception{
 MainActivity a=t.a;JSONObject fixture=new JSONObject(new String(Files.readAllBytes(new File(a.getFilesDir(),"unlink-fixture.json").toPath()),StandardCharsets.UTF_8)),one=fixture.getJSONObject("one"),two=fixture.getJSONObject("two");
 if(phase.equals("syncUnlinkRestart")){waitDone(t);t.check(a.sync.state.optString("tenant").equals(one.getString("tenant")),"PDV A restored after actual process restart");t.check(SyncFlow.find(a,"clients","RASCUNHO EXCLUSIVO A")!=null,"original tenant draft preserved across restart");t.check(SyncFlow.find(a,"clients","RASCUNHO EXCLUSIVO B")==null,"PDV B data absent after restart in A");t.ui(()->a.integration());t.click("DESVINCULAR PDV");dialog(t,"DESVINCULAR");waitDone(t);deny();t.check(a.sync.status().startsWith("NÃO VINCULADO"),"final handset detached from server and local state");return;}
 pair(t,one.getString("code"));t.check(a.sync.state.optString("tenant").equals(one.getString("tenant")),"paired to isolated PDV A");
 // Keep an unsent draft and operation UUID to prove no transfer to the next company.
 t.ui(()->{a.sync.main.removeCallbacks(a.sync.tickJob);a.store.add("clients").put("name","RASCUNHO EXCLUSIVO A");a.store.save();a.sync.prepare();a.integration();});String oldId=SyncFlow.find(a,"clients","RASCUNHO EXCLUSIVO A").getString("id");
 t.click("DESVINCULAR PDV");dialog(t,"CANCELAR");t.check(a.sync.state.optString("tenant").equals(one.getString("tenant")),"cancel keeps current PDV and drafts");
 // Actual UI, controlled network loss: do not claim server revocation while offline.
 t.ui(()->a.sync.transport=new SyncTransport.Exchange(){public JSONObject call(String p,JSONObject b)throws Exception{throw new IOException("OFFLINE TEST");}public byte[] download(String u)throws Exception{throw new IOException();}public void upload(String u,byte[] b)throws Exception{throw new IOException();}});
 t.click("DESVINCULAR PDV");dialog(t,"DESVINCULAR");waitDone(t);t.check(a.sync.state.optBoolean("detaching")&&!a.sync.state.optString("tenant").isEmpty(),"offline unlink remains pending, does not falsely clear grant");t.check(a.store.find("clients",oldId)!=null,"offline failure preserves old draft");
 t.ui(()->a.sync.transport=SyncTransport.HTTPS);t.click("DESVINCULAR PDV");dialog(t,"DESVINCULAR");waitDone(t);t.check(a.sync.state.optString("tenant").isEmpty(),"remote unlink completed without previous PDV running");deny();t.check(a.store.list("clients").length()==0&&a.store.list("projects").length()==0,"old company removed from active workspace and preserved in profile");t.check(t.find(a.root,"CONECTAR AO PDV")!=null,"new code entry immediately available");
 pair(t,PairingFlow.pdv(two,"/sync/ticket").getString("code"));t.check(a.sync.state.optString("tenant").equals(two.getString("tenant")),"switch to different PDV B with native code field");t.check(a.store.find("clients",oldId)==null,"PDV A draft and outbox do not enter B");
 t.ui(()->{a.sync.main.removeCallbacks(a.sync.tickJob);a.store.add("clients").put("name","RASCUNHO EXCLUSIVO B");a.store.save();a.sync.prepare();});
 t.click("DESVINCULAR PDV");dialog(t,"DESVINCULAR");waitDone(t);deny();
 pair(t,PairingFlow.pdv(one,"/sync/ticket").getString("code"));t.check(a.sync.state.optString("tenant").equals(one.getString("tenant")),"reconnect original PDV A");t.check(a.store.find("clients",oldId)!=null,"original draft UUID restored from A profile");t.check(SyncFlow.find(a,"clients","RASCUNHO EXCLUSIVO B")==null,"B draft never mixed into A");t.screenshot("proof-unlink-native");
 }
}
