package br.com.lealinfo.vidraceiro;
import android.app.*;import android.content.*;import android.os.*;import org.json.*;import java.io.*;import java.nio.file.Files;import java.nio.charset.StandardCharsets;import java.util.*;
/** Disposable QA package only. No real endpoint, identity registration or production grant. */
public class QaIntegrationInstrumentation extends Instrumentation {
 MainActivity a;StringBuilder log=new StringBuilder();
 interface Op {void run()throws Exception;}
 void ui(Op op){final Throwable[] e={null};runOnMainSync(()->{try{op.run();}catch(Throwable x){e[0]=x;}});waitForIdleSync();if(e[0]!=null)throw new AssertionError(e[0]);}
 void check(boolean b,String s){if(!b)throw new AssertionError(s);log.append("PASS ").append(s).append('\n');}
 void done()throws Exception{long end=System.currentTimeMillis()+10000;while(System.currentTimeMillis()<end){Thread.sleep(50);waitForIdleSync();if(!a.sync.busy)return;}throw new AssertionError("worker timeout");}
 @Override public void onCreate(Bundle b){super.onCreate(b);start();}
 @Override public void onStart(){Bundle out=new Bundle();try{
  check(getTargetContext().getPackageName().endsWith(".angraqa005"),"isolated QA applicationId");
  a=(MainActivity)startActivitySync(new Intent(getTargetContext(),MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
  final String tenant="synthetic-qa005-old",other="synthetic-qa005-new";final byte[] jpeg=new byte[]{(byte)255,(byte)216,1,2,3,4,5,6,7,8,9,10,11,12,(byte)255,(byte)217};final File[] photo={null};final String[] pending={null};
  ui(()->{a.sync.main.removeCallbacks(a.sync.tickJob);a.store.data=SyncCoordinator.emptyData();a.store.save();a.sync.state=new JSONObject().put("links",new JSONObject()).put("tenant",tenant).put("name","SYNTHETIC PC-CASA");for(int i=0;i<5;i++){JSONObject project=a.store.add("projects").put("title","QA pending "+i);JSONObject payload=new JSONObject().put("title","QA pending "+i);String operation="stable-operation-"+i;a.sync.link("projects",project.getString("id")).put("revision",1).put("baseline",payload).put("pending",new JSONObject().put("operationId",operation).put("payload",payload));if(i==0){PhotoAttachment.save(a,project,jpeg);photo[0]=new File(PhotoAttachment.directory(a,project),"photo.jpg");}}a.store.save();a.sync.save();pending[0]=a.sync.state.getJSONObject("links").toString();a.sync.transport=new Fake(true);a.integration();a.sync.unlink();});done();
  check(a.sync.state.optBoolean("detaching"),"DNS failure persists pending unlink rather than false success");check(a.store.list("projects").length()==5,"five synthetic pending projects preserved while offline");check(Arrays.equals(jpeg,Files.readAllBytes(photo[0].toPath())),"photo bytes preserved on network failure");
  ui(()->{a.sync.close();a.sync=new SyncCoordinator(a,a.store);a.sync.transport=new Fake(false);a.sync.nextDetachAttempt=0;a.sync.tick();});done();
  check(a.sync.state.optString("tenant").isEmpty(),"pending unlink resumes after coordinator restart and confirmed server response");
  JSONObject snapshot=new JSONObject(new String(a.sync.profile(tenant).readFully(),StandardCharsets.UTF_8));check(snapshot.getJSONObject("data").getJSONArray("projects").length()==5,"all pending projects archived under original tenant");check(snapshot.getJSONObject("state").getJSONObject("links").toString().equals(pending[0]),"operation IDs and revisions preserved exactly");check(Arrays.equals(jpeg,Files.readAllBytes(photo[0].toPath())),"unlink does not delete archived photos");
  ui(()->{a.sync.main.removeCallbacks(a.sync.tickJob);a.sync.state.put("switchTo",new JSONObject().put("tenant",other).put("name","QA005")).put("activationData",new JSONObject(a.store.data.toString()));a.sync.finishActivation();});
  check(a.store.list("projects").length()==0&&a.sync.links().length()==0,"old tenant projects and outbox never sent to new tenant");
  ui(()->{a.sync.state.put("switchTo",new JSONObject().put("tenant",tenant).put("name","OLD")).put("activationData",new JSONObject(a.store.data.toString()));a.sync.finishActivation();a.sync.main.removeCallbacks(a.sync.tickJob);});
  check(a.store.list("projects").length()==5&&a.sync.links().toString().equals(pending[0]),"reconnect restores same IDs, pending payloads and revisions");check(Arrays.equals(jpeg,Files.readAllBytes(photo[0].toPath())),"original photo recoverable after tenant round trip");
  ui(()->{a.sync.close();a.integration();});getUiAutomation().takeScreenshot().compress(android.graphics.Bitmap.CompressFormat.PNG,100,new FileOutputStream(new File(a.getFilesDir(),"qa-integration.png")));
  log.append("PASS ANDROID_QA005_INTEGRATION\n");Files.write(new File(a.getFilesDir(),"qa-integration.txt").toPath(),log.toString().getBytes(StandardCharsets.UTF_8));out.putString("stream",log.toString());ui(()->a.finish());finish(Activity.RESULT_OK,out);
 }catch(Throwable e){StringWriter sw=new StringWriter();e.printStackTrace(new PrintWriter(sw));out.putString("stream",log+"FAIL "+sw);finish(Activity.RESULT_CANCELED,out);}}
 static class Fake implements SyncTransport.Exchange {final boolean offline;Fake(boolean o){offline=o;}public JSONObject call(String p,JSONObject b)throws Exception{if(!p.equals("/sync/unlink"))throw new AssertionError("Unexpected network route: "+p);if(offline)throw new java.net.UnknownHostException("SYNTHETIC DNS FAILURE");return new JSONObject().put("revoked",true);}public byte[] download(String u){throw new AssertionError("No live download");}public void upload(String u,byte[] b){throw new AssertionError("No live upload");}}
}
