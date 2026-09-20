using System.Globalization;
using System.Text;

namespace LealInfoPDV;

internal static class PixEmv
{
    public static string NormalizeKey(string raw, string type)
    {
        var key=(raw??"").Trim();
        var t=(type??"").ToUpperInvariant();
        if(t is "CPF" or "CNPJ") key=new string(key.Where(char.IsDigit).ToArray());
        else if(t=="TELEFONE")
        {
            var digits=new string(key.Where(char.IsDigit).ToArray());
            key=digits.StartsWith("55") ? "+"+digits : "+55"+digits;
        }
        return key;
    }

    public static string BuildPayload(string rawKey, string type, decimal amount, string merchantName, string city, string txid="***")
    {
        var key=NormalizeKey(rawKey,type);
        if(string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Chave PIX não configurada.");
        var gui=Field("00","BR.GOV.BCB.PIX");
        var mai=Field("26",gui+Field("01",key));
        var name=Sanitize(merchantName,25,"LEAL INFO");
        var merchantCity=Sanitize(city,15,"BRASIL");
        var value=amount.ToString("0.00",CultureInfo.InvariantCulture);
        var additional=Field("62",Field("05",string.IsNullOrWhiteSpace(txid)?"***":txid));
        var payload="000201"+"010212"+mai+"52040000"+"5303986"+Field("54",value)+"5802BR"+Field("59",name)+Field("60",merchantCity)+additional+"6304";
        return payload+Crc16(payload);
    }

    private static string Field(string id,string value)=>id+Encoding.UTF8.GetByteCount(value).ToString("00",CultureInfo.InvariantCulture)+value;
    private static string Sanitize(string? value,int max,string fallback)
    {
        var s=(value??"").Normalize(NormalizationForm.FormD);
        s=new string(s.Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC).ToUpperInvariant();
        s=new string(s.Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='.').ToArray()).Trim();
        if(string.IsNullOrWhiteSpace(s)) s=fallback;
        return s.Length>max?s[..max]:s;
    }
    private static string Crc16(string text)
    {
        ushort crc=0xFFFF;
        foreach(var b in Encoding.UTF8.GetBytes(text))
        {
            crc^=(ushort)(b<<8);
            for(int i=0;i<8;i++) crc=(ushort)(((crc&0x8000)!=0)?(crc<<1)^0x1021:crc<<1);
        }
        return crc.ToString("X4",CultureInfo.InvariantCulture);
    }
}
