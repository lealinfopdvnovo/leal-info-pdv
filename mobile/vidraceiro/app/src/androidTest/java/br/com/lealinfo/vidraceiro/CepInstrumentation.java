package br.com.lealinfo.vidraceiro;
import android.app.*;import android.content.*;import android.os.*;import org.json.*;
/** Native UI test against real ViaCEP; same frozen production code as integration candidate. */
public final class CepInstrumentation extends MeasureFlowInstrumentation {
 @Override public void onStart(){Bundle report=new Bundle();try{
  a=(MainActivity)startActivitySync(new Intent(getTargetContext(),MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));waitForIdleSync();
  if(args!=null&&args.getString("phase","").equals("verify")){
   JSONObject auto=findClient("CEP TESTE AUTOMÁTICO"),manual=findClient("CEP TESTE MANUAL");check(auto!=null&&auto.optString("cep").equals("01001000")&&auto.optString("address").contains("10"),"CEP structured fields and composed address persisted after real process restart");check(manual!=null&&manual.optString("address").equals("ENDEREÇO MANUAL PRESERVADO"),"manual fallback persisted after restart");
  }else{
   ui(()->a.contactEditor("clients",null,false));set("CEP","01001000");long until=System.currentTimeMillis()+22000;while(System.currentTimeMillis()<until){Thread.sleep(300);waitForIdleSync();if(!input("Rua / logradouro").getText().toString().isEmpty())break;}
   check(!input("Rua / logradouro").getText().toString().isEmpty()&&input("Cidade").getText().toString().equals("São Paulo")&&input("UF").getText().toString().equals("SP"),"real ViaCEP HTTPS filled street/city/UF through native debounce");
   set("Nome","CEP TESTE AUTOMÁTICO");set("Número","10");set("Complemento","Sala 1");screenshot("proof-cep-native");click("SALVAR CADASTRO");JSONObject auto=findClient("CEP TESTE AUTOMÁTICO");check(auto!=null&&auto.optString("address").contains("10")&&auto.optString("street").length()>0,"native save composes address and retains CEP fields");
   ui(()->a.contactEditor("clients",null,false));set("CEP","01001000");set("Rua / logradouro","RUA MANUAL");Thread.sleep(12000);waitForIdleSync();check(input("Rua / logradouro").getText().toString().equals("RUA MANUAL"),"late CEP response does not overwrite manual street");set("CEP","000");set("Nome","CEP TESTE MANUAL");set("Endereço","ENDEREÇO MANUAL PRESERVADO");click("SALVAR CADASTRO");check(findClient("CEP TESTE MANUAL").optString("address").equals("ENDEREÇO MANUAL PRESERVADO"),"invalid/incomplete CEP never prevents manual save");
  }
  report.putString("stream",log+"PASS NATIVE_CEP\n");ui(()->a.finish());finish(Activity.RESULT_OK,report);
 }catch(Throwable e){java.io.StringWriter s=new java.io.StringWriter();e.printStackTrace(new java.io.PrintWriter(s));report.putString("stream",log+"FAIL NATIVE_CEP: "+s);finish(Activity.RESULT_CANCELED,report);}}
 JSONObject findClient(String name)throws Exception{JSONArray list=a.store.list("clients");for(int i=0;i<list.length();i++)if(list.getJSONObject(i).optString("name").equals(name))return list.getJSONObject(i);return null;}
}
