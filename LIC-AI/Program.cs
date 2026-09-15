using LicAi.Core;
using LicAi.Memory;
using LicAi.Security;

namespace LicAi;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var singleInstance = new Mutex(true, "LealInfoConectado_LIC_AI", out var firstInstance);
        if (!firstInstance) return;
        ApplicationConfiguration.Initialize();

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LealInfoConectado",
            "LIC-AI");
        Directory.CreateDirectory(appData);

        var memory = new ConversationMemory(Path.Combine(appData, "lic-memory.db"));
        var secrets = new LocalSecretStore(Path.Combine(appData, "settings.dat"));
        var client = new OpenAiClient(() => secrets.GetApiKey());
        var navigationClient = new NavigationAssistantClient(() => secrets.GetApiKey());
        var engine = new ConversationEngine(memory, client, navigationClient);

        var voiceOnly = args.Any(a => a.Equals("--voice", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(engine, secrets, voiceOnly));
    }
}
