package br.com.lealinfo.vidraceiro;
import org.json.*;import java.io.*;import java.nio.file.Files;import java.util.*;import android.graphics.*;
/** Real HTTPS on isolated GitHub-OIDC tenant. Fault injection only for offline transport. */
final class SyncFlow {
 static void run(MeasureFlowInstrumentation t,String phase)throws Exception{
  MainActivity a=t.a;
  if(phase.equals("syncPairing")){PairingFlow.run(t);return;}

  if(phase.equals("syncIdentity")){JSONObject identity=SecureGateway.identity();Files.write(new File(a.getFilesDir(),"sync-identity.json").toPath(),identity.toString().getBytes(java.nio.charset.StandardCharsets.UTF_8));return;}
  if(phase.equals("syncReopen")){waitSync(t);t.check(!a.sync.state.optString("tenant").isEmpty(),"authorized tenant persisted in new Android process");t.check(find(a,"clients","CLIENTE TESTE APP")!=null,"new app contact persisted in new process");t.ui(()->t.check(a.sync.prepare().length()==0,"no pending duplicate after process restart"));return;}
  JSONObject status=SyncTransport.request("/sync/status",new JSONObject());final JSONObject[] contact={null},project={null};
  if(phase.equals("syncLive")){
t.check(status.getString("tenant").startsWith("test-"),"real authorized isolated test tenant");t.ui(()->{a.sync.state.put("tenant",status.getString("tenant")).put("name","TESTE AUTOMATIZADO");a.sync.save();a.sync.tick();});waitSync(t);
  JSONObject bob=find(a,"clients","BOB");t.check(bob!=null&&!bob.isNull("pdvId"),"BOB from real PDV SQLite found in Android store");t.ui(()->a.contacts("clients",false));t.set("Localizar por nome ou documento","BOB");t.check(t.find(a.root,"BOB\n24999990000")!=null,"BOB visible through native contact search");
  t.ui(()->{a.home();contact[0]=a.store.add("clients").put("name","CLIENTE TESTE APP").put("document","QA-APP").put("phone","24988880000").put("email","").put("address","Endereço de teste");a.store.save();});
  // Offline failure preserves durable operation UUID and local draft; no fake server response.
  t.ui(()->{a.sync.transport=new SyncTransport.Exchange(){public JSONObject call(String p,JSONObject b)throws Exception{throw new IOException("TESTE OFFLINE CONTROLADO");}public byte[] download(String u)throws Exception{throw new IOException();}public void upload(String u,byte[] b)throws Exception{throw new IOException();}};a.sync.tick();});waitSync(t);
  final String[] operation={null};t.ui(()->{JSONObject pending=a.sync.link("clients",contact[0].getString("id")).getJSONObject("pending");operation[0]=pending.getString("operationId");t.check(operation[0].equals(a.sync.prepare().getJSONObject(0).getString("operationId")),"offline retry preserves operation UUID");});waitSync(t);
  return;
  }
  t.check(phase.equals("syncResume"),"resume phase after genuine process stop");waitSync(t);contact[0]=find(a,"clients","CLIENTE TESTE APP");t.check(contact[0]!=null,"offline customer restored in new Android process");t.ui(()->{a.sync.transport=SyncTransport.HTTPS;a.sync.tick();});waitSync(t);
  t.check(!contact[0].optString("id").isEmpty(),"app contact uploaded");
  t.ui(()->{contact[0].put("phone","24977770000");a.store.save();a.sync.tick();});waitSync(t);
  t.ui(()->{a.projects("project");});t.click("+ NOVO PROJETO");t.set("Nome do projeto","PROJETO APP SYNC BOX");t.ui(()->{a.type.setText("Box de banheiro",false);for(MainActivity.MeasureRow row:a.rows){row.value.setText("");row.unit.setSelection(2);} });t.ui(()->{a.rows.get(0).value.setText("2");a.rows.get(1).value.setText("2");a.flush();project[0]=a.current;project[0].put("clientLocalId",contact[0].getString("id"));a.validateProject(project[0]);
   try(InputStream photo=t.getContext().getAssets().open("bathroom-cc-by-sa.jpg")){PhotoAttachment.save(a,project[0],SyncTransport.read(photo,8000000));}a.store.save();a.home();a.sync.tick();});waitSync(t);Thread.sleep(4000);
  t.check(a.measure(project[0],"Largura").toPlainString().equals("2000"),"2 m remains 2000 mm through integration");t.ui(()->a.sync.tick());waitSync(t);t.ui(()->{t.check(a.sync.prepare().length()==0,"real ACK empties durable outbox without duplicates");});
  Files.write(new File(a.getFilesDir(),"sync-result.json").toPath(),new JSONObject().put("client",contact[0].getString("id")).put("project",project[0].getString("id")).put("sha",project[0].getJSONObject("photoAttachment").getString("sha256")).put("tenant",status.getString("tenant")).toString().getBytes(java.nio.charset.StandardCharsets.UTF_8));
 }
 static JSONObject find(MainActivity a,String entity,String name)throws Exception{JSONArray list=a.store.list(entity);for(int i=0;i<list.length();i++)if(name.equals(list.getJSONObject(i).optString("name")))return list.getJSONObject(i);return null;}
 static void waitSync(MeasureFlowInstrumentation t)throws Exception{long until=System.currentTimeMillis()+90000;while(System.currentTimeMillis()<until){Thread.sleep(250);t.waitForIdleSync();if(!t.a.sync.busy){if(!t.a.sync.state.optString("error").isEmpty()&&!t.a.sync.state.optString("error").contains("TESTE OFFLINE"))throw new Exception(t.a.sync.status());return;}}throw new Exception("sync timeout");}
}
