package br.com.lealinfo.vidraceiro;
import android.app.*;import android.content.*;import android.os.*;import org.json.*;import java.io.*;import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.util.*;
public class Reconfigure37Instrumentation extends Instrumentation {
 MainActivity a;StringBuilder log=new StringBuilder();interface Op{void run()throws Exception;}
 void ui(Op op){final Throwable[] e={null};runOnMainSync(()->{try{op.run();}catch(Throwable x){e[0]=x;}});waitForIdleSync();if(e[0]!=null)throw new AssertionError(e[0]);}
 void check(boolean b,String m){if(!b)throw new AssertionError(m);log.append("PASS ").append(m).append('\n');}
 @Override public void onCreate(Bundle b){super.onCreate(b);start();}
 @Override public void onStart(){Bundle out=new Bundle();try{
 check(getTargetContext().getPackageName().equals("br.com.lealinfo.vidraceiro.ambiente37"),"separate QA37 applicationId");
 check(new File(getTargetContext().getFilesDir(),"upgrade37-marker").exists(),"in-place installation retained private files");
 a=(MainActivity)startActivitySync(new Intent(getTargetContext(),MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
 final byte[] jpeg=new byte[]{(byte)255,(byte)216,1,2,3,4,5,6,7,8,9,10,11,12,(byte)255,(byte)217};final File[] photo={null};final File[] archive={null};
 ui(()->{a.sync.close();a.sync.state.put("tenant","old-test-003").put("name","OLD SYNTHETIC 003").remove("environment");for(int i=0;i<5;i++){JSONObject p=a.store.add("projects");p.put("title","Old test "+i);a.sync.link("projects",p.getString("id")).put("pending",new JSONObject().put("operationId","old-stable-"+i));if(i==0){PhotoAttachment.save(a,p,jpeg);photo[0]=new File(PhotoAttachment.directory(a,p),"photo.jpg");}}a.store.save();a.sync.save();a.sync=new SyncCoordinator(a,a.store);archive[0]=a.sync.profile("old-test-003").getBaseFile();});
 check(a.sync.state.optBoolean("migrationRequired"),"old tenant quarantined before any QA37 exchange");
 ui(()->{a.sync.busy=true;a.sync.reconfigureLocal();});
 check(a.sync.state.optString("tenant").isEmpty(),"reset works while old sync busy without contacting old server");
 JSONObject snapshot=new JSONObject(new String(Files.readAllBytes(archive[0].toPath()),StandardCharsets.UTF_8));check(snapshot.getJSONObject("data").getJSONArray("projects").length()==5,"all five pending projects archived");check(snapshot.getJSONObject("state").getJSONObject("links").length()==5,"stable pending operations archived");
 check(Arrays.equals(jpeg,Files.readAllBytes(photo[0].toPath())),"old photo bytes preserved");check(a.store.list("projects").length()==0&&a.sync.links().length()==0,"no automatic old data sent to new PDV");
 ui(()->{a.sync.close();a.sync=new SyncCoordinator(a,a.store);});check(a.sync.state.optString("tenant").isEmpty()&&"QA37".equals(a.sync.state.optString("environment")),"reset persists after coordinator restart");
 ui(()->{a.sync.close();a.sync.state.put("localReset",true).put("tenant","crash-test");a.sync.save();a.sync=new SyncCoordinator(a,a.store);});check(a.sync.state.optString("tenant").isEmpty(),"interrupted reset resumes durably");
 check(SyncTransport.ORIGIN.equals("https://angra-vidros-qa37.adriansantosangra.chatgpt.site"),"new endpoint only");
 ui(()->{a.sync.close();a.integration();});log.append("PASS ANGRA37_RECONFIGURE_COMPLETE\n");Files.write(new File(a.getFilesDir(),"qa37-result.txt").toPath(),log.toString().getBytes(StandardCharsets.UTF_8));out.putString("stream",log.toString());ui(()->a.finish());finish(Activity.RESULT_OK,out);
 }catch(Throwable e){StringWriter w=new StringWriter();e.printStackTrace(new PrintWriter(w));out.putString("stream",log+"FAIL "+w);finish(Activity.RESULT_CANCELED,out);}}
}
