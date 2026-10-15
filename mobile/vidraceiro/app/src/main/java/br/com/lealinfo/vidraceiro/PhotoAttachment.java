package br.com.lealinfo.vidraceiro;
import android.content.Context;import android.util.AtomicFile;import org.json.*;import java.io.*;import java.security.MessageDigest;import java.util.UUID;
/** Stable project-owned JPEG and minimal future-transfer metadata. Never a temporary camera URI. */
final class PhotoAttachment {
 static File directory(Context c,JSONObject p){File d=new File(c.getFilesDir(),"images/"+p.optString("id"));d.mkdirs();return d;}
 static JSONObject save(Context c,JSONObject p,byte[] jpeg)throws Exception{File dir=directory(c,p);AtomicFile file=new AtomicFile(new File(dir,"photo.jpg"));FileOutputStream out=null;try{out=file.startWrite();out.write(jpeg);file.finishWrite(out);}catch(Exception e){if(out!=null)file.failWrite(out);throw e;}byte[] hash=MessageDigest.getInstance("SHA-256").digest(jpeg);StringBuilder hex=new StringBuilder();for(byte b:hash)hex.append(String.format(java.util.Locale.ROOT,"%02x",b&255));JSONObject record=new JSONObject().put("id",UUID.randomUUID().toString()).put("projectId",p.getString("id")).put("fileName","photo.jpg").put("mimeType","image/jpeg").put("sizeBytes",jpeg.length).put("sha256",hex.toString()).put("capturedAt",System.currentTimeMillis()).put("transferState","LOCAL_ATTACHMENT_PENDING_UPLOAD");p.put("photoAttachment",record).put("hasPhotoAttachment",true);return record;}
 static void remove(Context c,JSONObject p)throws Exception{new AtomicFile(new File(directory(c,p),"photo.jpg")).delete();p.remove("photoAttachment");p.put("hasPhotoAttachment",false);}
}
