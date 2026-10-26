using System;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace LealInfo.IFood.Assistant
{
    internal static class Program
    {
        [STAThread] private static void Main()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => ReportFailure(e.Exception);
                AssistantForm.Run();
            }
            catch (Exception ex) { ReportFailure(ex); }
        }
        private static void ReportFailure(Exception ex)
        {
            // No exception message/stack, input value, HTTP body or header is exported.
            var text = new StringBuilder("LEAL INFO - FALHA LOCAL DO ASSISTENTE R6\r\n");
            text.AppendLine("UTC: " + DateTimeOffset.UtcNow.ToString("O"));
            text.AppendLine("CLR: " + Environment.Version);
            text.AppendLine("Processo 64 bits: " + Environment.Is64BitProcess);
            for (int n = 0; ex != null && n < 4; n++, ex = ex.InnerException)
            {
                text.AppendLine("Tipo: " + ex.GetType().FullName);
                text.AppendLine("HRESULT: 0x" + ex.HResult.ToString("X8"));
            }
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ERRO_IFOOD_TESTE.txt");
            try
            {
                File.WriteAllText(file, text.ToString(), new UTF8Encoding(true));
                MessageBox.Show("Nao foi possivel abrir ou continuar o assistente.\nEnvie ao Work o arquivo ERRO_IFOOD_TESTE.txt salvo na Area de Trabalho.\nO arquivo nao contem credenciais.", "LEAL INFO - Diagnostico", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                MessageBox.Show(text.ToString(), "LEAL INFO - Diagnostico local", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
