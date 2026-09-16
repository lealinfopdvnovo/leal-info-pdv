using LicAi.Core;
using LicAi.Memory;
using LicAi.Security;

namespace LicAi;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            ApplicationConfiguration.Initialize();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ShowCriticalError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var ex = e.ExceptionObject as Exception
                    ?? new Exception("Falha não tratada: " + e.ExceptionObject);
                ShowCriticalError(ex);
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                ShowCriticalError(e.Exception);
                e.SetObserved();
            };

            using var singleInstance = new Mutex(true, "LealInfoConectado_LIC_AI", out var firstInstance);
            if (!firstInstance)
            {
                MessageBox.Show(
                    "Já existe uma instância do LicAi.exe em execução.",
                    "ERRO CRÍTICO LIC-AI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

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
        catch (Exception ex)
        {
            ShowCriticalError(ex);
        }
    }

    private static void ShowCriticalError(Exception ex)
    {
        try
        {
            MessageBox.Show(
                ex.ToString(),
                "ERRO CRÍTICO LIC-AI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // Última barreira: evita uma segunda exceção mascarar a falha original.
        }
    }
}
