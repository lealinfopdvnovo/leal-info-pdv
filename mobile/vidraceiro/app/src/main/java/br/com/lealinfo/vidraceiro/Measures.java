package br.com.lealinfo.vidraceiro;
import java.math.BigDecimal;
public final class Measures {
 private Measures(){}
 public static BigDecimal parse(String s){ if(s==null || s.length()>80 || !s.trim().matches("[0-9]+([.,][0-9]+)?"))throw new IllegalArgumentException("Informe uma medida maior que zero (ex.: 2, 2,5 ou 2.5).");BigDecimal v=new BigDecimal(s.trim().replace(',','.'));if(v.signum()<=0)throw new IllegalArgumentException("A medida deve ser maior que zero.");return v; }
 public static BigDecimal factor(String u){switch(u){case "mm":return BigDecimal.ONE;case "cm":return BigDecimal.TEN;case "m":return new BigDecimal("1000");default:throw new IllegalArgumentException("Unidade inválida.");}}
 public static String mm(String s,String u){return parse(s).multiply(factor(u)).stripTrailingZeros().toPlainString();}
 public static String convert(String s,String from,String to){return parse(s).multiply(factor(from)).divide(factor(to)).stripTrailingZeros().toPlainString().replace('.',',');}
}
