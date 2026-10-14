package br.com.lealinfo.vidraceiro;
import org.junit.Test;
import static org.junit.Assert.*;
public class MeasuresTest {
 @Test public void invariant(){assertEquals("120",Measures.convert("1200","mm","cm"));assertEquals("1,2",Measures.convert("120","cm","m"));assertEquals("1200",Measures.convert("1,2","m","mm"));assertEquals("1000",Measures.mm("1","m"));assertEquals("1200",Measures.mm("120","cm"));for(int i=0;i<100;i++)assertEquals("1234,56789",Measures.convert(Measures.convert("1234,56789","mm","m"),"m","mm"));}
 @Test public void reject(){for(String x:new String[]{"","-2","NaN","1.000,2","0"}){try{Measures.parse(x);fail(x);}catch(IllegalArgumentException expected){}}}
}
