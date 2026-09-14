using LicAi.Core;
using LicAi.Memory;
using LicAi.Security;

namespace LicAi;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LealInfoConectado",
            "LIC-AI");
        Directory.CreateDirectory(appData);

        var memory = new ConversationMemory(Path.Combine(appData, "lic-memory.db"));
        var secrets = new LocalSecretStore(Path.Combine(appData, "settings.dat"));
        var client = new OpenAiClient(() => secrets.GetApiKey());
        var engine = new ConversationEngine(memory, client);

        Application.Run(new MainForm(engine, secrets));
    }
}
