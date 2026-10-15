package br.com.lealinfo.vidraceiro;
import android.app.*;import android.content.*;import android.graphics.BitmapFactory;import android.os.Bundle;import android.widget.Button;import org.json.*;import java.io.*;import java.lang.reflect.*;import java.nio.file.Files;import java.nio.charset.StandardCharsets;
final class NetworkFlow {
 private static Object field(Object o,String name)throws Exception{Field f=o.getClass().getDeclaredField(name);f.setAccessible(true);return f.get(o);}
 private static Object fieldUnchecked(Object o,String name){try{return field(o,name);}catch(Exception e){throw new AssertionError(e);}}
 static void run(Instrumentation test,String phase){Bundle report=new Bundle();try{
  Context context=test.getTargetContext();
  if("prepare".equals(phase)){
   JSONObject identity=SecureGateway.identity(),state=SecureGateway.authorize();if(!identity.getString("deviceId").equals(state.getString("deviceId")))throw new AssertionError("Device identity mismatch");
   Files.write(new File(context.getFilesDir(),"network-device.json").toPath(),identity.toString().getBytes(StandardCharsets.UTF_8));report.putString("stream","PASS NETWORK_PREPARE public-key identity generated on emulator; private key remains AndroidKeyStore\n");test.finish(Activity.RESULT_OK,report);return;
  }
  if(!"generate".equals(phase))throw new AssertionError("Unknown phase");
  String id="cbf9a848-7426-4bad-9f31-7b8a4c3839d2";File dir=new File(context.getFilesDir(),"images/"+id);dir.mkdirs();
  try(InputStream in=test.getContext().getAssets().open("bathroom-cc-by-sa.jpg")){Files.copy(in,new File(dir,"photo.jpg").toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING);}
  JSONObject draft=new JSONObject().put("id",id).put("title","Dona Maria").put("type","Box de banheiro").put("model","").put("configuration","Box de vidro transparente com painéis verticais e porta. Preservar o ambiente.").put("revision",1).put("simulationId","").put("measures",new JSONArray().put(new JSONObject().put("label","Largura").put("value","2").put("unit","m")).put(new JSONObject().put("label","Altura").put("value","2").put("unit","m")));
  Files.write(new File(dir,"draft.json").toPath(),draft.toString().getBytes(StandardCharsets.UTF_8));context.getSharedPreferences("MainActivity",0).edit().putString("current",id).commit();
  MainActivity a=(MainActivity)test.startActivitySync(new Intent(context,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));test.waitForIdleSync();
  // Activity.getPreferences uses local class name MainActivity on Android.
  if(!id.equals(field(a,"id"))){final MainActivity initial=a;test.runOnMainSync(()->{initial.getPreferences(0).edit().putString("current",id).commit();initial.finish();});a=(MainActivity)test.startActivitySync(new Intent(context,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));test.waitForIdleSync();}
  final MainActivity activity=a;final Button button=(Button)field(a,"create");test.runOnMainSync(button::performClick);test.waitForIdleSync();
  long deadline=android.os.SystemClock.uptimeMillis()+175000;String simulation="";
  while(android.os.SystemClock.uptimeMillis()<deadline){simulation=(String)field(activity,"simulation");if(!simulation.isEmpty())break;if(!(Boolean)field(activity,"busy"))break;Thread.sleep(500);}
  test.waitForIdleSync();
  if(simulation.isEmpty())throw new AssertionError("Real HTTPS generation did not complete: "+field(activity,"status"));
  File output=new File((File)field(activity,"dir"),"simulation.png");byte[] image=Files.readAllBytes(output.toPath());if(BitmapFactory.decodeByteArray(image,0,image.length)==null)throw new AssertionError("Image cannot be displayed");
  if(SecureGateway.hash(image).equals(SecureGateway.hash(Files.readAllBytes(new File(dir,"photo.jpg").toPath()))))throw new AssertionError("Original image returned unchanged");
  android.widget.ImageView displayed=(android.widget.ImageView)field(activity,"result");if(displayed.getDrawable()==null)throw new AssertionError("Result ImageView has no drawable");
  test.runOnMainSync(()->{android.widget.ScrollView scroll=(android.widget.ScrollView)((android.view.View)fieldUnchecked(activity,"root")).getParent();scroll.scrollTo(0,displayed.getTop()-80);});test.waitForIdleSync();
  Thread.sleep(300);android.graphics.Bitmap screen=test.getUiAutomation().takeScreenshot();if(screen==null)throw new AssertionError("No actual Android screenshot");try(FileOutputStream out=new FileOutputStream(new File(context.getFilesDir(),"network-screen.png"))){screen.compress(android.graphics.Bitmap.CompressFormat.PNG,100,out);}
  JSONObject evidence=new JSONObject().put("endpoint",SecureGateway.ENDPOINT).put("simulationId",simulation).put("imageSha256",SecureGateway.hash(image)).put("bytes",image.length).put("scenario","Dona Maria / Box / 2m x 2m / real licensed photograph");
  Files.write(new File(context.getFilesDir(),"network-result.json").toPath(),evidence.toString().getBytes(StandardCharsets.UTF_8));
  report.putString("stream","PASS REAL_NETWORK_IMAGE_UI: actual CREATE button -> signed HTTPS -> free Space -> returned image decoded and displayed. "+evidence+"\n");test.finish(Activity.RESULT_OK,report);
 }catch(Throwable error){report.putString("stream","FAIL REAL_NETWORK_IMAGE_UI: "+error);test.finish(Activity.RESULT_CANCELED,report);}}
}
