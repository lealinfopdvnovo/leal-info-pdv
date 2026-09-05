using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV;

public sealed record UserSession(long Id, string FullName, string Username, string Role, string Email, string Phone, bool CanDiscount);

public static class Auth
{
    public static UserSession? Current { get; private set; }

    public static int UserCount()
    {
        using var cn=Database.Open();
        using var cmd=cn.CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM users";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public static (string Hash,string Salt) HashPassword(string password)
    {
        byte[] salt=RandomNumberGenerator.GetBytes(16);
        byte[] hash=Rfc2898DeriveBytes.Pbkdf2(password, salt, 120000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password,string hash64,string salt64)
    {
        try
        {
            byte[] salt=Convert.FromBase64String(salt64);
            byte[] expected=Convert.FromBase64String(hash64);
            byte[] actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,120000,HashAlgorithmName.SHA256,32);
            return CryptographicOperations.FixedTimeEquals(actual,expected);
        }
        catch { return false; }
    }

    public static long CreateUser(string fullName,string username,string password,string role,string email,string phone,bool canDiscount=false)
    {
        var hp=HashPassword(password);
        using var cn=Database.Open();
        using var cmd=cn.CreateCommand();
        cmd.CommandText = "INSERT INTO users(full_name,username,password_hash,password_salt,role,email,phone,active,can_discount) " +
                          "VALUES($n,$u,$h,$s,$r,$e,$p,1,$d); " +
                          "SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n",fullName.Trim());
        cmd.Parameters.AddWithValue("$u",username.Trim());
        cmd.Parameters.AddWithValue("$h",hp.Hash);
        cmd.Parameters.AddWithValue("$s",hp.Salt);
        cmd.Parameters.AddWithValue("$r",role);
        cmd.Parameters.AddWithValue("$e",email.Trim());
        cmd.Parameters.AddWithValue("$p",phone.Trim());
        cmd.Parameters.AddWithValue("$d",canDiscount?1:0);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public static UserSession? Login(string username,string password)
    {
        using var cn=Database.Open();
        using var cmd=cn.CreateCommand();
        cmd.CommandText="SELECT id,full_name,username,password_hash,password_salt,role,COALESCE(email,''),COALESCE(phone,''),can_discount FROM users WHERE username=$u AND active=1";
        cmd.Parameters.AddWithValue("$u",username.Trim());
        using var rd=cmd.ExecuteReader();
        if(!rd.Read()) return null;
        if(!VerifyPassword(password,rd.GetString(3),rd.GetString(4))) return null;
        Current=new UserSession(rd.GetInt64(0),rd.GetString(1),rd.GetString(2),rd.GetString(5),rd.GetString(6),rd.GetString(7),rd.GetInt32(8)==1);
        return Current;
    }

    public static void Logout()=>Current=null;
    public static bool IsAdmin => Current?.Role=="ADMINISTRADOR";
    public static bool IsManager => Current?.Role=="GERENTE" || IsAdmin;
    public static string OperatorName => Current?.FullName ?? "ADMIN";

    public static void ResetPassword(long userId,string newPassword)
    {
        var hp=HashPassword(newPassword);
        using var cn=Database.Open();
        using var cmd=cn.CreateCommand();
        cmd.CommandText="UPDATE users SET password_hash=$h,password_salt=$s WHERE id=$id";
        cmd.Parameters.AddWithValue("$h",hp.Hash);
        cmd.Parameters.AddWithValue("$s",hp.Salt);
        cmd.Parameters.AddWithValue("$id",userId);
        cmd.ExecuteNonQuery();
    }

    static string EmergencyCodeHash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("LEALINFO-EMERGENCY|" + code.Trim().ToUpperInvariant()));
        return Convert.ToHexString(bytes);
    }

    public static List<string> GenerateEmergencyCodes(long userId, int count = 8)
    {
        using var cn = Database.Open();

        using (var del = cn.CreateCommand())
        {
            del.CommandText = "DELETE FROM emergency_recovery_codes WHERE user_id=$id";
            del.Parameters.AddWithValue("$id", userId);
            del.ExecuteNonQuery();
        }

        var result = new List<string>();
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        for (int i = 0; i < count; i++)
        {
            string raw;
            do
            {
                Span<byte> bytes = stackalloc byte[8];
                RandomNumberGenerator.Fill(bytes);
                var sb = new StringBuilder();
                for (int j = 0; j < 8; j++)
                    sb.Append(chars[bytes[j] % chars.Length]);
                raw = sb.ToString(0, 4) + "-" + sb.ToString(4, 4);
            }
            while (result.Contains(raw));

            using var ins = cn.CreateCommand();
            ins.CommandText = "INSERT INTO emergency_recovery_codes(user_id,code_hash,used) VALUES($id,$h,0)";
            ins.Parameters.AddWithValue("$id", userId);
            ins.Parameters.AddWithValue("$h", EmergencyCodeHash(raw));
            ins.ExecuteNonQuery();
            result.Add(raw);
        }

        return result;
    }

    public static (bool ok, long userId, string message) ValidateEmergencyCode(string usernameOrEmail, string code)
    {
        using var cn = Database.Open();

        using var user = cn.CreateCommand();
        user.CommandText = "SELECT id FROM users WHERE active=1 AND (username=$q OR lower(email)=lower($q)) LIMIT 1";
        user.Parameters.AddWithValue("$q", usernameOrEmail.Trim());
        var obj = user.ExecuteScalar();
        if (obj == null)
            return (false, 0, "Usuário ou e-mail não encontrado.");

        long userId = Convert.ToInt64(obj);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT id,code_hash FROM emergency_recovery_codes WHERE user_id=$id AND used=0";
        cmd.Parameters.AddWithValue("$id", userId);

        using var rd = cmd.ExecuteReader();
        long matchedId = 0;
        string wanted = EmergencyCodeHash(code);

        while (rd.Read())
        {
            var stored = rd.GetString(1);
            if (string.Equals(stored, wanted, StringComparison.OrdinalIgnoreCase))
            {
                matchedId = rd.GetInt64(0);
                break;
            }
        }
        rd.Close();

        if (matchedId == 0)
            return (false, 0, "Código de emergência inválido ou já utilizado.");

        using var use = cn.CreateCommand();
        use.CommandText = "UPDATE emergency_recovery_codes SET used=1 WHERE id=$id";
        use.Parameters.AddWithValue("$id", matchedId);
        use.ExecuteNonQuery();

        return (true, userId, "Código de emergência validado.");
    }

    public static int RemainingEmergencyCodes(long userId)
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM emergency_recovery_codes WHERE user_id=$id AND used=0";
        cmd.Parameters.AddWithValue("$id", userId);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }


    public static string RecoveryKeyPath =>
        Path.Combine(AppContext.BaseDirectory, "Dados", "recuperacao.leal");

    static byte[] RecoveryFileKey()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(
            "LEALINFO|RECOVERY-FILE|" + Database.DeviceSerial()));
    }

    static string EncryptRecoveryFile(string plain)
    {
        using var aes=Aes.Create();
        aes.Key=RecoveryFileKey();
        aes.GenerateIV();
        using var enc=aes.CreateEncryptor();
        var data=Encoding.UTF8.GetBytes(plain);
        var cipher=enc.TransformFinalBlock(data,0,data.Length);
        return Convert.ToBase64String(aes.IV.Concat(cipher).ToArray());
    }

    static string DecryptRecoveryFile(string encoded)
    {
        var all=Convert.FromBase64String(encoded);
        using var aes=Aes.Create();
        aes.Key=RecoveryFileKey();
        aes.IV=all.Take(16).ToArray();
        using var dec=aes.CreateDecryptor();
        var plain=dec.TransformFinalBlock(all,16,all.Length-16);
        return Encoding.UTF8.GetString(plain);
    }

    public static void SaveLocalRecoveryKey(long userId, string usernameOrEmail, IEnumerable<string> codes)
    {
        var dir=Path.GetDirectoryName(RecoveryKeyPath)!;
        Directory.CreateDirectory(dir);
        var payload=userId.ToString()+"\n"+usernameOrEmail.Trim()+"\n"+string.Join("\n",codes);
        File.WriteAllText(RecoveryKeyPath,EncryptRecoveryFile(payload),Encoding.UTF8);
    }

    public static bool HasLocalRecoveryKey()
    {
        try { return File.Exists(RecoveryKeyPath) && new FileInfo(RecoveryKeyPath).Length>20; }
        catch { return false; }
    }

    public static (bool ok,long userId,string message) TryLocalRecovery(string usernameOrEmail)
    {
        if(!HasLocalRecoveryKey())
            return(false,0,"Chave de recuperação local não encontrada neste computador.");

        try
        {
            var payload=DecryptRecoveryFile(File.ReadAllText(RecoveryKeyPath,Encoding.UTF8));
            var lines=payload.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
            if(lines.Length<3 || !long.TryParse(lines[0],out var storedUserId))
                return(false,0,"Arquivo de recuperação local inválido.");

            string storedIdentity=lines[1];
            if(!string.Equals(storedIdentity,usernameOrEmail.Trim(),StringComparison.OrdinalIgnoreCase))
            {
                using var cn=Database.Open();
                using var cmd=cn.CreateCommand();
                cmd.CommandText="SELECT id FROM users WHERE id=$id AND active=1 AND (username=$q OR lower(email)=lower($q)) LIMIT 1";
                cmd.Parameters.AddWithValue("$id",storedUserId);
                cmd.Parameters.AddWithValue("$q",usernameOrEmail.Trim());
                if(cmd.ExecuteScalar()==null)
                    return(false,0,"A chave local não pertence a este usuário.");
            }

            foreach(var code in lines.Skip(2))
            {
                var r=ValidateEmergencyCode(usernameOrEmail,code);
                if(r.ok)
                {
                    RewriteLocalRecoveryWithout(code);
                    return(true,r.userId,"Chave local validada neste computador.");
                }
            }
            return(false,0,"A chave local não possui códigos válidos. Gere um novo conjunto ao entrar no sistema.");
        }
        catch
        {
            return(false,0,"Não foi possível abrir a chave local. Ela pode pertencer a outro computador ou estar corrompida.");
        }
    }

    static void RewriteLocalRecoveryWithout(string usedCode)
    {
        try
        {
            var payload=DecryptRecoveryFile(File.ReadAllText(RecoveryKeyPath,Encoding.UTF8));
            var lines=payload.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).ToList();
            if(lines.Count<3)return;
            lines.RemoveAll(x=>string.Equals(x,usedCode,StringComparison.OrdinalIgnoreCase));
            File.WriteAllText(RecoveryKeyPath,EncryptRecoveryFile(string.Join("\n",lines)),Encoding.UTF8);
        }
        catch { }
    }

}