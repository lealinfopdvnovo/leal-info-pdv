using System.Drawing;
using System.Windows.Forms;
namespace LealInfo.Components;

public sealed class ComponentUpdateForm : Form
{
    private bool applying;
    public ComponentUpdateForm(Installed installed, Selection selection, Func<Release,Task> apply)
    {
        Text="LEAL INFO — Atualizações por componente";
        StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new Size(840,390);MinimumSize=new Size(720,420);
        BackColor=Color.FromArgb(5,23,40);ForeColor=Color.White;
        Font=new Font("Segoe UI",11);AutoScaleMode=AutoScaleMode.Dpi;
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(20)};
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute,50));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));table.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
        Controls.Add(table);
        var title=new Label{Text="GLOBAL E PET • VERSÕES INDEPENDENTES",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font(Font.FontFamily,16,FontStyle.Bold)};
        table.Controls.Add(title,0,0);table.SetColumnSpan(title,2);
        void Card(ComponentKind kind,string name,string current,int column)
        {
            var release=selection.Available.SingleOrDefault(r=>r.Component==kind);
            var panel=new Panel{Dock=DockStyle.Fill,Margin=new Padding(8),Padding=new Padding(16),BackColor=Color.FromArgb(10,42,65)};
            table.Controls.Add(panel,column,1);
            var info=new Label{Dock=DockStyle.Top,Height=110,Text=name+"\nInstalado: "+current+"\n"+(release is null?"Sem atualização disponível":"Disponível: "+release.Version)};
            panel.Controls.Add(info);
            var button=new Button{Text="ATUALIZAR "+kind,Name="update"+kind,Dock=DockStyle.Bottom,Height=50,Enabled=release is not null,BackColor=Color.FromArgb(0,115,175),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
            panel.Controls.Add(button);
            button.Click+=async (_,_)=>{
                if(applying||release is null)return;
                applying=true;Enabled=false;
                try{await apply(release);DialogResult=DialogResult.OK;Close();}
                catch(Exception e){MessageBox.Show(this,e.Message,"Atualização não concluída",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
                finally{applying=false;if(!IsDisposed)Enabled=true;}
            };
        }
        Card(ComponentKind.GLOBAL,"GLOBAL • BASE COMUM",installed.GlobalVersion,0);
        Card(ComponentKind.PET,"PET • "+installed.Identity,installed.PetVersion,1);
        var note=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Text=selection.Errors.Count>0?string.Join("\n",selection.Errors):"Cada componente conserva sua própria versão.\nBackup e recuperação devem ser confirmados pelo aplicador antes da instalação."};
        table.Controls.Add(note,0,2);table.SetColumnSpan(note,2);
        FormClosing+=(_,e)=>{if(applying&&DialogResult!=DialogResult.OK)e.Cancel=true;};
    }
}
