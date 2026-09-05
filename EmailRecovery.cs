using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV;

public static class EmailRecovery
{
    static string Get(string key,string fallback="")
    {
        using var cn=Database.Open(); using var cmd=cn.CreateCommand();
        cmd.CommandText="SELECT value FROM settings WHERE key=$k"; cmd.Parameters.AddWithValue("$k",key);
        return Convert.ToString(cmd.ExecuteScalar()) ?? fallback;
    }
    static void Set(string key,string value)
    {
        using var cn=Database.Open(); using var cmd=cn.CreateCommand();
        cmd.CommandText="INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$k",key); cmd.Parameters.AddWithValue("$v",value??""); cmd.ExecuteNonQuery();
    }
    static byte[] LocalKey()=>SHA256.HashData(Encoding.UTF8.GetBytes("LEALINFO|SMTP|"+Database.DeviceSerial()));
    static string Encrypt(string plain)
    {
        if(string.IsNullOrEmpty(plain)) return "";
        using var aes=Aes.Create(); aes.Key=LocalKey(); aes.GenerateIV();
        using var enc=aes.CreateEncryptor(); var data=Encoding.UTF8.GetBytes(plain);
        var cipher=enc.TransformFinalBlock(data,0,data.Length);
        return Convert.ToBase64String(aes.IV.Concat(cipher).ToArray());
    }
    static string Decrypt(string encoded)
    {
        try{
            var all=Convert.FromBase64String(encoded); using var aes=Aes.Create(); aes.Key=LocalKey();
            aes.IV=all.Take(16).ToArray(); using var dec=aes.CreateDecryptor();
            var plain=dec.TransformFinalBlock(all,16,all.Length-16); return Encoding.UTF8.GetString(plain);
        }catch{return "";}
    }
    public static void SaveSmtp(string host,int port,string user,string password,bool ssl,string fromName)
    {
        Set("smtp_host",host.Trim());Set("smtp_port",port.ToString());Set("smtp_user",user.Trim());
        if(!string.IsNullOrWhiteSpace(password))Set("smtp_password",Encrypt(password));
        Set("smtp_ssl",ssl?"1":"0");Set("smtp_from_name",string.IsNullOrWhiteSpace(fromName)?"LEAL INFO PDV":fromName.Trim());
    }
    public static (string host,int port,string user,bool ssl,string fromName,bool hasPassword) GetSmtp()
    {
        int port=int.TryParse(Get("smtp_port","587"),out var p)?p:587;
        return(Get("smtp_host"),port,Get("smtp_user"),Get("smtp_ssl","1")=="1",Get("smtp_from_name","LEAL INFO PDV"),!string.IsNullOrWhiteSpace(Get("smtp_password")));
    }
    public static bool IsConfigured(){var s=GetSmtp();return !string.IsNullOrWhiteSpace(s.host)&&!string.IsNullOrWhiteSpace(s.user)&&s.hasPassword;}
    static string HashCode(string code)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("LEALINFO-RESET|"+code)));

    public static (bool ok,string message) SendResetCode(string q)
    {
        using var cn=Database.Open(); using var cmd=cn.CreateCommand();
        cmd.CommandText="SELECT id,full_name,COALESCE(email,'') FROM users WHERE active=1 AND (username=$q OR lower(email)=lower($q)) LIMIT 1";
        cmd.Parameters.AddWithValue("$q",q.Trim()); using var rd=cmd.ExecuteReader();
        if(!rd.Read())return(false,"Usuário ou e-mail não encontrado.");
        long uid=rd.GetInt64(0);string name=rd.GetString(1),email=rd.GetString(2);
        if(string.IsNullOrWhiteSpace(email))return(false,"Este usuário não possui e-mail de recuperação cadastrado.");
        if(!IsConfigured())return(false,"O envio por e-mail ainda não foi configurado pelo Administrador.");
        string code=RandomNumberGenerator.GetInt32(100000,1000000).ToString();
        string expires=DateTime.UtcNow.AddMinutes(10).ToString("O");
        rd.Close();
        using(var x=cn.CreateCommand()){x.CommandText="UPDATE password_reset_codes SET used=1 WHERE user_id=$id AND used=0";x.Parameters.AddWithValue("$id",uid);x.ExecuteNonQuery();}
        using(var x=cn.CreateCommand()){x.CommandText="INSERT INTO password_reset_codes(user_id,code_hash,expires_at,used) VALUES($id,$h,$e,0)";x.Parameters.AddWithValue("$id",uid);x.Parameters.AddWithValue("$h",HashCode(code));x.Parameters.AddWithValue("$e",expires);x.ExecuteNonQuery();}
        try{
            var s=GetSmtp();var pwd=Decrypt(Get("smtp_password"));
            using var mail=new MailMessage();mail.From=new MailAddress(s.user,s.fromName);mail.To.Add(email);
            mail.Subject="Código de recuperação - LEAL INFO PDV";
            mail.Body="Olá, "+name+".\r\n\r\nSeu código de recuperação é: "+code+"\r\n\r\nVálido por 10 minutos.\r\n\r\nLEAL INFO PDV";
            using var smtp=new SmtpClient(s.host,s.port){EnableSsl=s.ssl,UseDefaultCredentials=false,Credentials=new NetworkCredential(s.user,pwd),DeliveryMethod=SmtpDeliveryMethod.Network};
            smtp.Send(mail);return(true,"Código enviado para "+Mask(email));
        }catch(Exception ex){return(false,"Não foi possível enviar o e-mail.\r\n\r\n"+ex.Message);}
    }
    static string Mask(string email){int at=email.IndexOf('@');if(at<=1)return email;return email[0]+new string('*',Math.Max(2,at-1))+email[at..];}
    public static (bool ok,long userId,string message) ValidateCode(string q,string code)
    {
        using var cn=Database.Open();using var u=cn.CreateCommand();
        u.CommandText="SELECT id FROM users WHERE active=1 AND (username=$q OR lower(email)=lower($q)) LIMIT 1";u.Parameters.AddWithValue("$q",q.Trim());
        var o=u.ExecuteScalar();if(o==null)return(false,0,"Usuário não encontrado.");long uid=Convert.ToInt64(o);
        using var cmd=cn.CreateCommand();cmd.CommandText="SELECT id,code_hash,expires_at FROM password_reset_codes WHERE user_id=$id AND used=0 ORDER BY id DESC LIMIT 1";cmd.Parameters.AddWithValue("$id",uid);
        using var rd=cmd.ExecuteReader();if(!rd.Read())return(false,0,"Não existe código ativo.");
        long rid=rd.GetInt64(0);string h=rd.GetString(1),e=rd.GetString(2);
        if(!DateTime.TryParse(e,null,System.Globalization.DateTimeStyles.RoundtripKind,out var expiry)||DateTime.UtcNow>expiry.ToUniversalTime())return(false,0,"O código expirou. Solicite outro.");
        if(!string.Equals(h,HashCode(code.Trim()),StringComparison.OrdinalIgnoreCase))return(false,0,"Código inválido.");
        rd.Close();using var use=cn.CreateCommand();use.CommandText="UPDATE password_reset_codes SET used=1 WHERE id=$id";use.Parameters.AddWithValue("$id",rid);use.ExecuteNonQuery();
        return(true,uid,"Código validado.");
    }
}
