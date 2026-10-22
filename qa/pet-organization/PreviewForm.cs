namespace PetOrganization;
public sealed class PreviewForm:Form
{
 public DataGridView Before {get;}=Grid();
 public DataGridView After {get;}=Grid();
 public PreviewForm(PetCatalog catalog)
 {
  Text="LEAL INFO PDV PRO — ORGANIZAÇÃO DOS PETs — PRÉVIA";StartPosition=FormStartPosition.CenterScreen;ClientSize=new Size(1120,660);MinimumSize=new Size(900,600);BackColor=Color.FromArgb(5,15,28);ForeColor=Color.White;Font=new Font("Segoe UI",11);
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(20)};
  root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  foreach(var height in new[]{86,46,-1,76,58})root.RowStyles.Add(new RowStyle(height<0?SizeType.Percent:SizeType.Absolute,height<0?100:height));
  var title=new Label{Text="ORGANIZAÇÃO DOS PETs\nNovos nomes • versões independentes",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Gold,Font=new Font("Segoe UI",19,FontStyle.Bold)};root.Controls.Add(title,0,0);
  root.Controls.Add(new Label{Text="PRÉVIA DA PREPARAÇÃO — NÃO PUBLICADO — NÃO INSTALADO",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(255,190,65),Font=new Font("Segoe UI",11,FontStyle.Bold)},0,1);
  var panels=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));panels.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  Before.Columns.Add("Id","CÓDIGO");Before.Columns.Add("Name","ANTES: IDENTIFICAÇÃO");Before.Columns.Add("Pet","PET");Before.Columns[0].FillWeight=20;Before.Columns[1].FillWeight=55;Before.Columns[2].FillWeight=40;
  After.Columns.Add("Name","DEPOIS: NOME DO PET");After.Columns.Add("Pet","VERSÃO");After.Columns[0].FillWeight=76;After.Columns[1].FillWeight=24;
  foreach(var p in catalog.Profiles){Before.Rows.Add(p.InternalId,"Cliente "+p.InternalId,"Não definido");After.Rows.Add(p.DisplayName,"V"+p.PetVersion);}
  panels.Controls.Add(Panel("ANTES",Before,Color.FromArgb(21,105,155)),0,0);panels.Controls.Add(Panel("DEPOIS — PREPARADO",After,Color.FromArgb(125,91,18)),1,0);root.Controls.Add(panels,0,2);
  root.Controls.Add(new Label{Text="GLOBAL "+catalog.GlobalReferenceVersion+" — INALTERADO\nCada PET evolui sozinho: V1.1 → V1.2 → V1.3",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(75,215,255),Font=new Font("Segoe UI",12,FontStyle.Bold)},0,3);
  root.Controls.Add(new Label{Text="Códigos mantidos internamente para compatibilidade.\nBanco, licença, configurações e personalizações existentes permanecem intactos.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Silver},0,4);Controls.Add(root);
 }
 static Control Panel(string title,DataGridView grid,Color color)
 {
  var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(7)};p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));p.RowStyles.Add(new RowStyle(SizeType.Absolute,44));p.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  p.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,BackColor=color,ForeColor=Color.White,Font=new Font("Segoe UI",13,FontStyle.Bold)},0,0);p.Controls.Add(grid,0,1);return p;
 }
 static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,ColumnHeadersHeight=48,RowTemplate={Height=57},BackgroundColor=Color.FromArgb(10,28,44),BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,DefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.FromArgb(10,28,44),ForeColor=Color.White,SelectionBackColor=Color.FromArgb(10,28,44),SelectionForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold)},ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.FromArgb(21,48,67),ForeColor=Color.Gold,Font=new Font("Segoe UI",9,FontStyle.Bold)},GridColor=Color.FromArgb(33,69,87)};
}
