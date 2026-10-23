using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using LicAi.Security;

class Probe
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var type = typeof(LocalSecretStore).Assembly.GetType("LicAi.ApiKeyDialog")!;
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
                    if (dialog.Owner != owner || owner.Enabled || !dialog.Visible) throw new Exception("Modal owner failed");
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
            if (!owner.Enabled) throw new Exception("Owner remains disabled");
            if (action == "Save" && (result != DialogResult.OK || (string)type.GetProperty("ApiKey", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)! != dummy)) throw new Exception("Save failed");
            if (action != "Save" && result == DialogResult.OK) throw new Exception("Cancel failed");
            Console.WriteLine("PASS: modal owned, masked, responsive: " + action);
        }
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".bin");
        try
        {
            var store = new LocalSecretStore(path);
            store.SaveApiKey(dummy);
            if (System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(dummy)) throw new Exception("Plaintext secret");
            if (new LocalSecretStore(path).GetApiKey() != dummy) throw new Exception("DPAPI persistence failed");
            Console.WriteLine("PASS: DPAPI CurrentUser persistence without plaintext");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        owner.Close();
    }
}
