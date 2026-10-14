package br.com.lealinfo.vidraceiro;

import android.app.*;
import android.content.*;
import android.graphics.Bitmap;
import android.os.Bundle;
import android.view.accessibility.AccessibilityNodeInfo;
import android.widget.*;
import org.json.*;
import java.io.*;
import java.lang.reflect.*;
import java.util.*;

/** Drives actual Activity controls and the actual create-button handler on an isolated emulator. */
public class MeasureFlowInstrumentation extends Instrumentation {
    private MainActivity activity;
    @Override public void onCreate(Bundle args) { super.onCreate(args); start(); }
    private Object field(Object object,String name) {
        try { Field f=object.getClass().getDeclaredField(name);f.setAccessible(true);return f.get(object); }
        catch(Exception error){throw new AssertionError(error);}
    }
    private void check(boolean ok,String reason){if(!ok)throw new AssertionError(reason);}
    private ArrayList<?> rows(){return (ArrayList<?>)field(activity,"rows");}
    private void ui(Runnable task){runOnMainSync(task);waitForIdleSync();}
    private JSONObject spec(){
        final JSONObject[] result={null};
        ui(()->{try{Method method=MainActivity.class.getDeclaredMethod("project");method.setAccessible(true);result[0]=(JSONObject)method.invoke(activity);}catch(Exception error){throw new AssertionError(error);}});
        return result[0];
    }
    private void fill(String value,String unit){
        ui(()->{for(Object row:rows())((EditText)field(row,"value")).setText("");});
        ui(()->{for(Object row:rows())((Spinner)field(row,"unit")).setSelection(Arrays.asList("mm","cm","m").indexOf(unit));});
        ui(()->{for(Object row:rows())((EditText)field(row,"value")).setText(value);});
    }
    private String tree(AccessibilityNodeInfo node){if(node==null)return "";StringBuilder result=new StringBuilder();if(node.getText()!=null)result.append(node.getText()).append('\n');for(int i=0;i<node.getChildCount();i++)result.append(tree(node.getChild(i)));return result.toString();}
    private boolean pressOK(AccessibilityNodeInfo node){if(node==null)return false;if("OK".contentEquals(node.getText()==null?"":node.getText()))return node.performAction(AccessibilityNodeInfo.ACTION_CLICK);for(int i=0;i<node.getChildCount();i++)if(pressOK(node.getChild(i)))return true;return false;}
    private void createExpect(String message)throws Exception{
        ui(()->((Button)field(activity,"create")).performClick());
        String actual="";long deadline=android.os.SystemClock.uptimeMillis()+5000;
        do{actual=tree(getUiAutomation().getRootInActiveWindow());if(actual.contains(message))break;Thread.sleep(100);}while(android.os.SystemClock.uptimeMillis()<deadline);
        check(actual.contains(message),"CREATE expected "+message+" got "+actual);
        check(pressOK(getUiAutomation().getRootInActiveWindow()),"close dialog");waitForIdleSync();
    }
    @Override public void onStart(){Bundle result=new Bundle();try{
        activity=(MainActivity)startActivitySync(new Intent(getTargetContext(),MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));waitForIdleSync();
        ui(()->{((EditText)field(activity,"title")).setText("Dona Maria");((AutoCompleteTextView)field(activity,"type")).setText("Box de banheiro",false);int i=0;for(Object row:rows())((EditText)field(row,"label")).setText(i++==0?"Largura":"Altura");activity.getSharedPreferences("connection",0).edit().clear().commit();});
        File photo=new File((File)field(activity,"dir"),"photo.jpg");try(FileOutputStream out=new FileOutputStream(photo)){Bitmap.createBitmap(20,20,Bitmap.Config.ARGB_8888).compress(Bitmap.CompressFormat.JPEG,90,out);}
        String[][] cases={{"A","2","m"},{"B","2,00","m"},{"C","2.00","m"},{"D","2000","mm"}};
        StringBuilder report=new StringBuilder();
        for(String[] item:cases){fill(item[1],item[2]);JSONArray dimensions=spec().getJSONArray("measures");for(int i=0;i<2;i++)check("2000".equals(dimensions.getJSONObject(i).getString("mm")),item[0]+" canonical");createExpect("Configure o serviço HTTPS");report.append("PASS ").append(item[0]).append(": controls -> canonical 2000mm -> CREATE -> service configuration stage\n");}
        fill("2","m");ui(()->((Spinner)field(rows().get(0),"unit")).setSelection(0));check("2000".equals(((EditText)field(rows().get(0),"value")).getText().toString()),"E 2m -> 2000mm");ui(()->((Spinner)field(rows().get(0),"unit")).setSelection(2));check("2".equals(((EditText)field(rows().get(0),"value")).getText().toString()),"E inverse");report.append("PASS E: 2m -> 2000mm -> 2m; height independent\n");
        fill("2","m");ui(()->((EditText)field(rows().get(1),"value")).setText(""));ui(()->((Spinner)field(rows().get(1),"unit")).setSelection(0));ui(()->((EditText)field(rows().get(1),"value")).setText("2"));JSONArray mixed=spec().getJSONArray("measures");check("2000".equals(mixed.getJSONObject(0).getString("mm"))&&"2".equals(mixed.getJSONObject(1).getString("mm")),"independent units");report.append("PASS independent units: 2m x 2mm = 2000mm x 2mm\n");
        String[][] invalid={{"F","0"},{"G","-2"},{"H","abc"},{"EMPTY",""}};
        for(String[] item:invalid){fill("2","m");ui(()->((EditText)field(rows().get(0),"value")).setText(item[1]));createExpect("Medida 1 (Largura): preencha VALOR DA MEDIDA");report.append("PASS ").append(item[0]).append(": invalid value blocked with field-specific error\n");}
        result.putString("stream","\n"+report+"PASS MEASURE_UI_FLOW\n");ui(()->activity.finish());finish(Activity.RESULT_OK,result);
    }catch(Throwable error){result.putString("stream","FAIL MEASURE_UI_FLOW: "+error);finish(Activity.RESULT_CANCELED,result);}}
}
