package br.com.lealinfo.vidraceiro;
import android.content.Context;import android.graphics.*;import android.graphics.drawable.Drawable;import android.view.View;
/** Native glass surfaces and line icons, not a screenshot masquerading as UI. */
final class HomeVisuals {
 static Bitmap facadeImage, moduleImage;
 static synchronized void images(Context c){if(facadeImage==null){facadeImage=BitmapFactory.decodeResource(c.getResources(),R.drawable.home_facade);moduleImage=BitmapFactory.decodeResource(c.getResources(),R.drawable.home_module_art);}}
 static final class Hero extends Drawable {
 final Paint p=new Paint(3);Hero(Context c){images(c);}
 public void draw(Canvas c){Rect b=getBounds();float w=b.width(),h=b.height();p.setAlpha(255);c.drawColor(0xff020e1c);float scale=Math.max(w/facadeImage.getWidth(),h/facadeImage.getHeight());float iw=facadeImage.getWidth()*scale,ih=facadeImage.getHeight()*scale;c.drawBitmap(facadeImage,null,new RectF(w-iw,0,w,ih),p);p.setShader(new LinearGradient(0,0,w,0,new int[]{0xf5020d1b,0xcc020d1b,0x10020d1b},new float[]{0,.48f,1},Shader.TileMode.CLAMP));c.drawRect(b,p);p.setShader(new LinearGradient(0,h*.7f,0,h,new int[]{0x00020d1b,0xff020d1b},null,Shader.TileMode.CLAMP));c.drawRect(b,p);p.setShader(null);}
 public void setAlpha(int a){}public void setColorFilter(ColorFilter f){}public int getOpacity(){return PixelFormat.OPAQUE;}
 }
 static final class Glass extends Drawable {
 final Paint p=new Paint(3);final int accent,index;final float radius,d;
 Glass(Context c,int color,boolean header){this(c,color,-1);}
 Glass(Context c,int color,int module){images(c);accent=color;index=module;d=c.getResources().getDisplayMetrics().density;radius=15*d;}
 @Override public void draw(Canvas c){Rect b=getBounds();float w=b.width(),h=b.height(),r=radius;RectF box=new RectF(b);c.save();Path clip=new Path();clip.addRoundRect(box,r,r,Path.Direction.CW);c.clipPath(clip);
 int dark=Color.rgb(Color.red(accent)/8,Color.green(accent)/8,Color.blue(accent)/8);int mid=Color.rgb(Color.red(accent)/3,Color.green(accent)/3,Color.blue(accent)/3);
 p.setAlpha(255);p.setShader(new LinearGradient(0,0,w,h,new int[]{mid,dark,0xff041120},null,Shader.TileMode.CLAMP));c.drawRect(box,p);p.setShader(null);
 if(index>=0){int cell=moduleImage.getWidth()/4;Rect src=new Rect(index%4*cell,index/4*cell,(index%4+1)*cell,(index/4+1)*cell);float sz=Math.min(h*1.08f,w*.72f);p.setAlpha(92);c.drawBitmap(moduleImage,src,new RectF(w-sz+8*d,-3*d,w+8*d,sz-3*d),p);p.setAlpha(255);p.setShader(new LinearGradient(0,0,w,0,new int[]{(dark&0xffffff)|0xa0000000,0x00000000},null,Shader.TileMode.CLAMP));c.drawRect(box,p);p.setShader(null);}
 p.setShader(new RadialGradient(0,h*.35f,Math.max(w,h),new int[]{(accent&0xffffff)|0x88000000,0x00000000},null,Shader.TileMode.CLAMP));c.drawRect(box,p);p.setShader(null);p.setColor(accent);c.drawRoundRect(new RectF(0,9*d,3*d,h-9*d),2*d,2*d,p);
 p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(6*d);p.setColor((accent&0xffffff)|0x18000000);c.drawRoundRect(new RectF(d,d,w-d,h-d),r,r,p);p.setStrokeWidth(3*d);p.setColor((accent&0xffffff)|0x30000000);c.drawRoundRect(new RectF(d,d,w-d,h-d),r,r,p);p.setStrokeWidth(d);p.setColor((accent&0xffffff)|0xb0000000);c.drawRoundRect(new RectF(.7f*d,.7f*d,w-.7f*d,h-.7f*d),r,r,p);
 p.setColor(0xff7bdcf2);p.setStrokeWidth(1.7f*d);Path arrow=new Path();arrow.moveTo(w-13*d,h*.50f-6*d);arrow.lineTo(w-7*d,h*.50f);arrow.lineTo(w-13*d,h*.50f+6*d);c.drawPath(arrow,p);p.setStyle(Paint.Style.FILL);c.restore();
 }
 @Override public void setAlpha(int a){p.setAlpha(a);}@Override public void setColorFilter(ColorFilter f){p.setColorFilter(f);}@Override public int getOpacity(){return PixelFormat.TRANSLUCENT;}
 }
 static final class Icon extends View {
 final Paint p=new Paint(3);final int index;Icon(Context c,int i){super(c);index=i;setLayerType(View.LAYER_TYPE_SOFTWARE,null);setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);}
 @Override protected void onDraw(Canvas c){super.onDraw(c);c.save();c.scale(getWidth()/48f,getHeight()/48f);p.setColor(0xffe4faff);p.setShadowLayer(3,0,0,0xff1fc8f5);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2f);p.setStrokeJoin(Paint.Join.ROUND);p.setStrokeCap(Paint.Cap.ROUND);
 switch(index){case 0:case 4:c.drawRoundRect(10,5,35,42,3,3,p);for(int y=17;y<35;y+=6)c.drawLine(16,y,29,y,p);if(index==0){p.setTextSize(17);p.setStyle(Paint.Style.FILL);c.drawText("$",18,20,p);}else c.drawLine(28,36,41,20,p);break;case 1:c.drawCircle(20,15,7,p);c.drawArc(6,25,34,46,180,180,false,p);c.drawCircle(35,17,5,p);c.drawArc(27,27,45,44,240,110,false,p);break;case 2:case 8:case 9:c.drawRoundRect(7,7,41,41,2,2,p);c.drawLine(24,7,24,41,p);if(index!=8)c.drawLine(7,24,41,24,p);c.drawLine(12,17,19,10,p);break;case 3:case 11:case 10:Path cube=new Path();cube.moveTo(24,5);cube.lineTo(42,15);cube.lineTo(24,25);cube.lineTo(6,15);cube.close();c.drawPath(cube,p);if(index==3){c.drawLine(6,25,24,35,p);c.drawLine(24,35,42,25,p);c.drawLine(6,33,24,43,p);c.drawLine(24,43,42,33,p);}else{c.drawLine(6,15,6,34,p);c.drawLine(6,34,24,44,p);c.drawLine(24,25,24,44,p);c.drawLine(24,44,42,34,p);c.drawLine(42,34,42,15,p);}break;case 5:for(int i=0;i<3;i++)c.drawRoundRect(7+i*13,30-i*10,15+i*13,42,1,1,p);break;case 6:c.drawRoundRect(9,4,39,44,3,3,p);c.drawRect(14,10,34,19,p);for(int y=26;y<40;y+=7)for(int x=16;x<35;x+=8)c.drawCircle(x,y,1,p);break;case 7:c.drawRoundRect(4,11,29,34,2,2,p);c.drawRect(29,22,43,34,p);c.drawCircle(13,37,5,p);c.drawCircle(35,37,5,p);break;default:c.drawRect(10,8,38,40,p);}
 p.setStyle(Paint.Style.FILL);c.restore();}
 }
}
