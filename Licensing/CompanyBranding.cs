using System.Drawing.Imaging;
namespace LealInfoPDV.Licensing;
internal static class CompanyBranding
{
    internal static event Action? Changed;
    private const string LogoSetting="company_logo_base64";
    internal static string Get(string key)
    {
        using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText="SELECT value FROM settings WHERE key=$k";cmd.Parameters.AddWithValue("$k",key);return cmd.ExecuteScalar()?.ToString()??"";
    }
    internal static void SetLogo(string path)
    {
        if(new FileInfo(path).Length > 10*1024*1024)throw new InvalidDataException("Escolha um logo de até 10 MB.");
        using var source=Image.FromFile(path);
        var scale=Math.Min(1d,1200d/Math.Max(source.Width,source.Height));
        using var normalized=new Bitmap(Math.Max(1,(int)(source.Width*scale)),Math.Max(1,(int)(source.Height*scale)));
        using(var g=Graphics.FromImage(normalized)){g.Clear(Color.Transparent);g.DrawImage(source,0,0,normalized.Width,normalized.Height);}
        using var bytes=new MemoryStream();normalized.Save(bytes,ImageFormat.Png);
        using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText="INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";cmd.Parameters.AddWithValue("$k",LogoSetting);cmd.Parameters.AddWithValue("$v",Convert.ToBase64String(bytes.ToArray()));cmd.ExecuteNonQuery();Changed?.Invoke();
    }
    internal static Image? LoadLogo(bool fallback=true)
    {
        try {var text=Get(LogoSetting);if(text.Length>0){using var stream=new MemoryStream(Convert.FromBase64String(text));using var image=Image.FromStream(stream);return new Bitmap(image);}}catch { }
        if(!fallback)return null;
        try {var path=Path.Combine(AppContext.BaseDirectory,"Assets","logo.png");using var image=Image.FromFile(path);return new Bitmap(image);}catch{return null;}
    }
    internal static void ChooseLogo(IWin32Window owner)
    {
        using var dialog=new OpenFileDialog {Title="Logotipo da empresa",Filter="Imagens|*.png;*.jpg;*.jpeg;*.bmp"};
        if(dialog.ShowDialog(owner)!=DialogResult.OK)return;
        try {SetLogo(dialog.FileName);MessageBox.Show(owner,"Logotipo salvo. A abertura e a tela principal usarão esta imagem.");}catch(Exception ex){MessageBox.Show(owner,ex.Message,"Logotipo",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
}
