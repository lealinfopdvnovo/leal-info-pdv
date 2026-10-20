using Protection003;
using System.Diagnostics;
internal static class Entry
{
    [STAThread] static void Main(){ApplicationConfiguration.Initialize();Application.Run(new ActivationForm());}
}
internal sealed class ActivationForm:Form
{
    readonly Label status=new(){Dock=DockStyle.Fill,Padding=new Padding(20),Text="CLIENTE 003 — PRIMEIRA TRANSIÇÃO SEGURA\n\nFeche o PDV no servidor e nos terminais.\nUse a mesma conta Windows do PDV.\n\nA verificação prepara backup e recuperação externa antes de qualquer troca. Não use ATUALIZAR na versão antiga durante esta etapa."};
    readonly Button prepare=new(){Text="1 — VERIFICAR E PREPARAR PROTEÇÃO",Dock=DockStyle.Top,Height=52};
    readonly Button apply=new(){Text="2 — CONCLUIR TRANSIÇÃO PROTEGIDA",Dock=DockStyle.Top,Height=52,Enabled=false};
    Core? core;
    internal ActivationForm(){Text="LEAL INFO — Proteção exclusiva 003";ClientSize=new Size(650,360);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(20,22,26);ForeColor=Color.Gold;Font=new Font("Segoe UI",11);var buttons=new Panel(){Dock=DockStyle.Bottom,Height=116,Padding=new Padding(10)};buttons.Controls.Add(apply);buttons.Controls.Add(prepare);Controls.Add(status);Controls.Add(buttons);prepare.Click+=async(_,_)=>await Prepare();apply.Click+=async(_,_)=>await Apply();}
    async Task Prepare(){prepare.Enabled=false;apply.Enabled=false;try{core=Core.Installed();await core.Prepare();status.Text="PREPARAÇÃO VALIDADA — 003\n\nBackup dos três arquivos conferido.\nRecuperação SYSTEM e integridade confirmadas.\nBanco, licença e configurações preservados.\n\nClique em CONCLUIR TRANSIÇÃO PROTEGIDA. A proteção será conferida novamente antes da troca. O PDV abrirá após a aplicação; se a abertura falhar, os executáveis anteriores serão recuperados.";apply.Enabled=true;}catch(Exception ex){status.Text="NÃO APLICADO\n\n"+ex.Message;}finally{prepare.Enabled=true;}}
    async Task Apply(){prepare.Enabled=false;apply.Enabled=false;try{using var p=Process.GetCurrentProcess();status.Text="Confirmando novamente backup e recuperação externa…";await core!.Handoff(p.Id,p.StartTime.ToUniversalTime().Ticks);Application.Exit();}catch(Exception ex){status.Text="TRANSIÇÃO NÃO AUTORIZADA\n\n"+ex.Message;prepare.Enabled=true;}}
}
