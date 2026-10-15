package br.com.lealinfo.vidraceiro;
import android.content.Context;import android.graphics.*;import android.graphics.drawable.Drawable;import android.view.View;
/** Native glass surfaces and line icons, not a screenshot masquerading as UI. */
final class HomeVisuals {
 static final class Glass extends Drawable {
 final Paint p=new Paint(3);final int accent;final boolean facade;final float radius;
 Glass(Context context,int color,boolean header){accent=color;facade=header;radius=18*context.getResources().getDisplayMetrics().density;}
 @Override public void draw(Canvas c){Rect b=getBounds();float w=b.width(),h=b.height(),r=radius;RectF box=new RectF(b);p.setShader(new LinearGradient(0,0,w,h,new int[]{0xff102e49,0xff092139,0xff061322},null,Shader.TileMode.CLAMP));c.drawRoundRect(box,r,r,p);p.setShader(null);
 if(facade){c.save();c.clipRect(b);for(int i=0;i<6;i++){float x=w*(.54f+i*.1f);Path glass=new Path();glass.moveTo(x,h*.02f);glass.lineTo(x+w*.08f,h*.13f);glass.lineTo(x+w*.08f,h*.92f);glass.lineTo(x,h);glass.close();p.setShader(new LinearGradient(x,0,x+w*.08f,h,new int[]{0x5532d7ff,0x07194769,0x661377b2},null,Shader.TileMode.CLAMP));c.drawPath(glass,p);p.setShader(null);p.setStyle(Paint.Style.STROKE);p.setColor(0x4469dfff);p.setStrokeWidth(1);c.drawPath(glass,p);p.setStyle(Paint.Style.FILL);}p.setShader(new LinearGradient(0,0,w,0,new int[]{0xff071729,0xec071729,0x22071729},new float[]{0,.47f,1},Shader.TileMode.CLAMP));c.drawRect(box,p);p.setShader(null);c.restore();}
 else{p.setShader(new RadialGradient(w*.1f,0,w,new int[]{(accent&0xffffff)|0x55000000,0x00000000},null,Shader.TileMode.CLAMP));c.drawRoundRect(box,r,r,p);p.setShader(null);p.setColor(accent);c.drawRoundRect(new RectF(b.left,b.top+14,b.left+4,b.bottom-14),3,3,p);p.setColor(0x1266e9ff);Path shine=new Path();shine.moveTo(w*.58f,0);shine.lineTo(w*.85f,0);shine.lineTo(w*.45f,h);shine.lineTo(w*.22f,h);shine.close();c.save();c.clipRect(b);c.drawPath(shine,p);c.restore();}
 p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(1.5f);p.setColor(0x8861cce8);c.drawRoundRect(new RectF(b.left+1,b.top+1,b.right-1,b.bottom-1),r,r,p);p.setStyle(Paint.Style.FILL);
 }
 @Override public void setAlpha(int a){p.setAlpha(a);}@Override public void setColorFilter(ColorFilter f){p.setColorFilter(f);}@Override public int getOpacity(){return PixelFormat.TRANSLUCENT;}
 }
 static final class Icon extends View {
 final Paint p=new Paint(3);final int index;Icon(Context c,int i){super(c);index=i;setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);}
 @Override protected void onDraw(Canvas c){super.onDraw(c);c.save();c.scale(getWidth()/48f,getHeight()/48f);p.setColor(0xffb5f2ff);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(1.8f);p.setStrokeJoin(Paint.Join.ROUND);p.setStrokeCap(Paint.Cap.ROUND);
 switch(index){case 0:case 4:c.drawRoundRect(10,5,35,42,3,3,p);for(int y=17;y<35;y+=6)c.drawLine(16,y,29,y,p);if(index==0){p.setTextSize(17);p.setStyle(Paint.Style.FILL);c.drawText("$",18,20,p);}else c.drawLine(28,36,41,20,p);break;case 1:c.drawCircle(20,15,7,p);c.drawArc(6,25,34,46,180,180,false,p);c.drawCircle(35,17,5,p);c.drawArc(27,27,45,44,240,110,false,p);break;case 2:case 8:case 9:c.drawRoundRect(7,7,41,41,2,2,p);c.drawLine(24,7,24,41,p);if(index!=8)c.drawLine(7,24,41,24,p);c.drawLine(12,17,19,10,p);break;case 3:case 11:case 10:Path cube=new Path();cube.moveTo(24,5);cube.lineTo(42,15);cube.lineTo(24,25);cube.lineTo(6,15);cube.close();c.drawPath(cube,p);if(index==3){c.drawLine(6,25,24,35,p);c.drawLine(24,35,42,25,p);c.drawLine(6,33,24,43,p);c.drawLine(24,43,42,33,p);}else{c.drawLine(6,15,6,34,p);c.drawLine(6,34,24,44,p);c.drawLine(24,25,24,44,p);c.drawLine(24,44,42,34,p);c.drawLine(42,34,42,15,p);}break;case 5:for(int i=0;i<3;i++)c.drawRoundRect(7+i*13,30-i*10,15+i*13,42,1,1,p);break;case 6:c.drawRoundRect(9,4,39,44,3,3,p);c.drawRect(14,10,34,19,p);for(int y=26;y<40;y+=7)for(int x=16;x<35;x+=8)c.drawCircle(x,y,1,p);break;case 7:c.drawRoundRect(4,11,29,34,2,2,p);c.drawRect(29,22,43,34,p);c.drawCircle(13,37,5,p);c.drawCircle(35,37,5,p);break;default:c.drawRect(10,8,38,40,p);}
 p.setStyle(Paint.Style.FILL);c.restore();}
 }
}
