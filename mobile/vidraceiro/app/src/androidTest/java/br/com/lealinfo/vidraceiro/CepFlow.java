package br.com.lealinfo.vidraceiro;
import org.json.*;
final class CepFlow {
 static void run(MeasureFlowInstrumentation t,String phase)throws Exception{
  MainActivity a=t.a;
  if(phase.equals("cepVerify")){
   JSONObject auto=find(a,"CEP TESTE AUTOMÁTICO"),manual=find(a,"CEP TESTE MANUAL");t.check(auto!=null&&auto.optString("cep").equals("01001000")&&auto.optString("address").contains("10"),"CEP fields/address persisted after process restart");t.check(manual!=null&&manual.optString("address").equals("ENDEREÇO MANUAL PRESERVADO"),"manual CEP fallback persisted after restart");return;
  }
  t.ui(()->a.contactEditor("clients",null,false));t.set("CEP","01001000");long until=System.currentTimeMillis()+22000;while(System.currentTimeMillis()<until){Thread.sleep(300);t.waitForIdleSync();if(!t.input("Rua / logradouro").getText().toString().isEmpty())break;}
  t.check(!t.input("Rua / logradouro").getText().toString().isEmpty()&&t.input("Cidade").getText().toString().equals("São Paulo")&&t.input("UF").getText().toString().equals("SP"),"real ViaCEP HTTPS fills street/city/UF through native debounce");
  t.set("Nome","CEP TESTE AUTOMÁTICO");t.set("Número","10");t.set("Complemento","Sala 1");t.screenshot("proof-cep-native");t.click("SALVAR CADASTRO");JSONObject auto=find(a,"CEP TESTE AUTOMÁTICO");t.check(auto!=null&&auto.optString("address").contains("10")&&auto.optString("street").length()>0,"native save composes address and retains structured CEP fields");
  t.ui(()->a.contactEditor("clients",null,false));t.set("CEP","01001000");t.set("Rua / logradouro","RUA MANUAL");Thread.sleep(12000);t.waitForIdleSync();t.check(t.input("Rua / logradouro").getText().toString().equals("RUA MANUAL"),"late CEP response preserves manual edit");t.set("CEP","000");t.set("Nome","CEP TESTE MANUAL");t.set("Endereço","ENDEREÇO MANUAL PRESERVADO");t.click("SALVAR CADASTRO");t.check(find(a,"CEP TESTE MANUAL").optString("address").equals("ENDEREÇO MANUAL PRESERVADO"),"incomplete CEP never blocks manual save");
 }
 static JSONObject find(MainActivity a,String name)throws Exception{JSONArray list=a.store.list("clients");for(int i=0;i<list.length();i++)if(list.getJSONObject(i).optString("name").equals(name))return list.getJSONObject(i);return null;}
}
