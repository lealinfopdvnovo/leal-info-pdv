package br.com.lealinfo.vidraceiro;
import java.math.*;import java.text.NumberFormat;import java.util.Locale;
/** Decimal-only arithmetic. Prices are entered by the professional, never inferred. */
public final class Commercial {
 private Commercial(){}
 public static BigDecimal amount(String text){String s=text==null?"":text.trim().replace("R$","").replace("\u00a0","").replace(" ","");if(s.isEmpty())return BigDecimal.ZERO;if(s.contains(",")){if(!s.matches("(?:[0-9]+|[0-9]{1,3}(?:\\.[0-9]{3})+),[0-9]{1,2}"))throw new IllegalArgumentException("Informe dinheiro como 100,50 ou 100.");s=s.replace(".","").replace(',','.');}else if(!s.matches("[0-9]+(?:\\.[0-9]{1,2})?"))throw new IllegalArgumentException("Valor monetário inválido.");BigDecimal n=new BigDecimal(s);if(n.precision()>16)throw new IllegalArgumentException("Valor monetário muito grande.");return n;}
 public static BigDecimal area(String widthMm,String heightMm){return Measures.parse(widthMm).multiply(Measures.parse(heightMm)).divide(new BigDecimal("1000000"));}
 public static BigDecimal total(BigDecimal area,int quantity,BigDecimal price,BigDecimal service,BigDecimal discount,BigDecimal extra){if(quantity<1||quantity>1000)throw new IllegalArgumentException("Quantidade: 1 a 1000.");BigDecimal t=area.multiply(BigDecimal.valueOf(quantity)).multiply(price).add(service).subtract(discount).add(extra).setScale(2,RoundingMode.HALF_UP);if(t.signum()<0)throw new IllegalArgumentException("Desconto supera o valor do orçamento.");return t;}
 public static String money(BigDecimal n){return NumberFormat.getCurrencyInstance(new Locale("pt","BR")).format(n);}
}
