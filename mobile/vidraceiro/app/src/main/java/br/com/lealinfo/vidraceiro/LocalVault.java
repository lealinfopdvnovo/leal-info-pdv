package br.com.lealinfo.vidraceiro;
import java.nio.charset.StandardCharsets;import javax.crypto.Cipher;import javax.crypto.spec.GCMParameterSpec;import android.util.Base64;
/** Versioned authenticated encryption. Legacy plaintext is read once and atomically migrated on save. */
final class LocalVault {
 static byte[] encode(String plain)throws Exception{Cipher c=Cipher.getInstance("AES/GCM/NoPadding");c.init(Cipher.ENCRYPT_MODE,Vault.key());return ("AV37ENC1:"+Base64.encodeToString(c.getIV(),2)+":"+Base64.encodeToString(c.doFinal(plain.getBytes(StandardCharsets.UTF_8)),2)).getBytes(StandardCharsets.UTF_8);}
 static String decode(byte[] bytes)throws Exception{String s=new String(bytes,StandardCharsets.UTF_8);if(!s.startsWith("AV37ENC1:"))return s;String[] p=s.split(":",3);Cipher c=Cipher.getInstance("AES/GCM/NoPadding");c.init(Cipher.DECRYPT_MODE,Vault.key(),new GCMParameterSpec(128,Base64.decode(p[1],2)));return new String(c.doFinal(Base64.decode(p[2],2)),StandardCharsets.UTF_8);}
}
