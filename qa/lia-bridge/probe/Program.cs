using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;


class Probe
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool IsWindowEnabled(IntPtr hwnd);
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));
        if (assembly.Location != Path.GetFullPath(args[0])) throw new Exception("Wrong delivered assembly");
        var type=assembly.GetType("LicAi.ApiKeyDialog")!;
        Console.WriteLine("PASS: delivered LIA assembly loaded from PDV LIC-AI path");
        using var owner = new Form { TopMost = true };
        owner.Show();
        string dummy = "sk-isolated-dummy-not-a-real-key-123456789";
        foreach (var action in new[] { "Save", "Cancel", "Close" })
        {
            using var dialog = (Form)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "Teste", "Chave" }, null)!;
            Exception? failure = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    if (dialog.Owner != owner || IsWindowEnabled(owner.Handle) || !dialog.Visible) throw new Exception("Modal owner failed");
                    var key = (TextBox)dialog.Controls.Find("ApiKey", true).Single();
                    if (!key.UseSystemPasswordChar) throw new Exception("Unmasked key");
                    key.Text = dummy;
                    if (action == "Close") dialog.Close();
                    else ((Button)dialog.Controls.Find(action, true).Single()).PerformClick();
                }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            };
            timer.Start();
            var result = dialog.ShowDialog(owner);
            if (failure != null) throw failure;
            if (!IsWindowEnabled(owner.Handle)) throw new Exception("Owner remains disabled");
            if (action == "Save" && (result != DialogResult.OK || (string)type.GetProperty("ApiKey", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)! != dummy)) throw new Exception("Save failed");
            if (action != "Save" && result == DialogResult.OK) throw new Exception("Cancel failed");
            Console.WriteLine("PASS: modal owned, masked, responsive: " + action);
        }
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".bin");
        try
        {
            var secretType=assembly.GetType("LicAi.Security.LocalSecretStore")!;
            var store=Activator.CreateInstance(secretType, path)!;
            secretType.GetMethod("SaveApiKey")!.Invoke(store,new object[]{dummy});
            if (System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(dummy)) throw new Exception("Plaintext secret");
            if ((string?)secretType.GetMethod("GetApiKey")!.Invoke(Activator.CreateInstance(secretType,path),null) != dummy) throw new Exception("DPAPI persistence failed");
            Console.WriteLine("PASS: DPAPI CurrentUser persistence without plaintext");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        owner.Close();
    }
}
