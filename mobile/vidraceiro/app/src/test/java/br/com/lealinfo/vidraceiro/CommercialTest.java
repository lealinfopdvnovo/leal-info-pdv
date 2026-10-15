package br.com.lealinfo.vidraceiro;
import org.junit.Test;import static org.junit.Assert.*;import java.math.BigDecimal;
public class CommercialTest {
 @Test public void real132(){BigDecimal area=Commercial.area("1200","1000");assertEquals(0,new BigDecimal("1.2").compareTo(area));assertEquals("132.00",Commercial.total(area,1,Commercial.amount("100,00"),BigDecimal.ZERO,BigDecimal.ZERO,Commercial.amount("12,00")).toPlainString());}
 @Test public void brazilian(){assertEquals("100.50",Commercial.amount("100,50").toPlainString());assertEquals("100.50",Commercial.amount("100.50").toPlainString());assertEquals("1000.50",Commercial.amount("R$ 1.000,50").toPlainString());assertEquals(BigDecimal.ZERO,Commercial.amount(""));for(String x:new String[]{"-1","NaN","1,2.3","abc","1,234","Infinity"}){try{Commercial.amount(x);fail(x);}catch(IllegalArgumentException expected){}}}
 @Test public void unitsSameArea(){for(String v:new String[]{"2","2,00","2.00"})assertEquals(0,new BigDecimal("4").compareTo(Commercial.area(Measures.mm(v,"m"),Measures.mm(v,"m"))));}
}
