package br.com.lealinfo.vidraceiro;
import android.content.Context;import android.security.keystore.KeyGenParameterSpec;import android.security.keystore.KeyProperties;import android.util.Base64;
import java.io.*;import java.net.*;import java.nio.charset.StandardCharsets;import java.security.*;import java.security.spec.ECGenParameterSpec;import java.util.UUID;import org.json.JSONObject;
final class SecureGateway {
 static final String ENDPOINT="https://angra-vidros-gateway.classyglobe9.chatgpt.site";
 private static final String ALIAS="angra-vidros-device-v1";
 static final class AuthorizationRequired extends Exception {final String url;AuthorizationRequired(String url){super("Autorize este celular uma única vez para criar simulações.");this.url=url;}}
 private static synchronized KeyStore keys()throws Exception{KeyStore k=KeyStore.getInstance("AndroidKeyStore");k.load(null);if(!k.containsAlias(ALIAS)){KeyPairGenerator g=KeyPairGenerator.getInstance(KeyProperties.KEY_ALGORITHM_EC,"AndroidKeyStore");g.initialize(new KeyGenParameterSpec.Builder(ALIAS,KeyProperties.PURPOSE_SIGN|KeyProperties.PURPOSE_VERIFY).setAlgorithmParameterSpec(new ECGenParameterSpec("secp256r1")).setDigests(KeyProperties.DIGEST_SHA256).build());g.generateKeyPair();}return k;}
 static JSONObject identity()throws Exception{byte[] publicKey=keys().getCertificate(ALIAS).getPublicKey().getEncoded();return new JSONObject().put("publicKey",Base64.encodeToString(publicKey,Base64.NO_WRAP)).put("deviceId",hash(publicKey));}
 static JSONObject authorize()throws Exception{return post("/device/register",new JSONObject().put("publicKey",identity().getString("publicKey")),false);}
 static JSONObject request(Context context,String path,JSONObject body)throws Exception{JSONObject state=authorize();if(!state.optBoolean("approved"))throw new AuthorizationRequired(state.getString("authorizationUrl"));return post(path,body,true);}
 static String hash(byte[] value)throws Exception{byte[] d=MessageDigest.getInstance("SHA-256").digest(value);StringBuilder out=new StringBuilder();for(byte b:d)out.append(String.format(java.util.Locale.ROOT,"%02x",b&255));return out.toString();}
 private static JSONObject post(String path,JSONObject body,boolean signed)throws Exception{
  byte[] bytes=body.toString().getBytes(StandardCharsets.UTF_8);HttpURLConnection c=(HttpURLConnection)new URL(ENDPOINT+path).openConnection();c.setInstanceFollowRedirects(false);c.setRequestMethod("POST");c.setConnectTimeout(15000);c.setReadTimeout(180000);c.setDoOutput(true);c.setFixedLengthStreamingMode(bytes.length);c.setRequestProperty("Content-Type","application/json");c.setRequestProperty("User-Agent","ANGRA-VIDROS/0.1.5 Android");
  if(signed){String stamp=String.valueOf(System.currentTimeMillis()),nonce=UUID.randomUUID().toString(),id=identity().getString("deviceId");String canonical="POST\n"+path+"\n"+stamp+"\n"+nonce+"\n"+hash(bytes);Signature signature=Signature.getInstance("SHA256withECDSA");signature.initSign((PrivateKey)keys().getKey(ALIAS,null));signature.update(canonical.getBytes(StandardCharsets.UTF_8));c.setRequestProperty("X-Angra-Device",id);c.setRequestProperty("X-Angra-Time",stamp);c.setRequestProperty("X-Angra-Nonce",nonce);c.setRequestProperty("X-Angra-Signature",Base64.encodeToString(signature.sign(),Base64.NO_WRAP));}
  try{try(OutputStream out=c.getOutputStream()){out.write(bytes);}int code=c.getResponseCode();InputStream stream=code==200?c.getInputStream():c.getErrorStream();if(stream==null)throw new IOException("Serviço indisponível ("+code+").");JSONObject response;
   try(InputStream in=stream;ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] buf=new byte[8192];int n;while((n=in.read(buf))!=-1){if(out.size()+n>20000000)throw new IOException("Resposta muito grande.");out.write(buf,0,n);}try{response=new JSONObject(out.toString("UTF-8"));}catch(Exception bad){throw new IOException("O serviço não devolveu uma resposta válida ("+code+").");}}
   if(code!=200)throw new IOException(response.optString("error","Serviço indisponível ("+code+")."));return response;
  }finally{c.disconnect();}
 }
}
