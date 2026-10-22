using LealInfoPDV;
public sealed class LojaTestExtension : IPetExtension
{
 public void Customize(Form form)
 {
  if(form.GetType().Name=="MainForm"){
   const string old=" — LOJA LEAL INFO • PET V1.1";
   const string marker=" — LOJA LEAL INFO • PET V1.2";
   if(form.Text.EndsWith(old,StringComparison.Ordinal))form.Text=form.Text[..^old.Length];
   if(!form.Text.EndsWith(marker,StringComparison.Ordinal))form.Text+=marker;
  }
  if(!form.Text.Contains("TELA DE VENDA",StringComparison.OrdinalIgnoreCase))return;
  if(form.Controls.Find("pet001_test_button",true).Length!=0)return;
  var style=Walk(form).OfType<Button>().FirstOrDefault(b=>b.Text.Trim()=="ESTILO");
  if(style?.Parent is not TableLayoutPanel panel||panel.ColumnCount!=1||panel.RowCount!=2)return;
  var position=panel.GetPositionFromControl(style);
  if(position.Column!=0||position.Row!=0)return;
  var close=panel.GetControlFromPosition(0,1);
  panel.SuspendLayout();
  try{
   panel.Controls.Remove(style);if(close!=null)panel.Controls.Remove(close);
   panel.Dock=DockStyle.Fill;
   panel.ColumnCount=2;panel.ColumnStyles.Clear();
   panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   panel.Controls.Add(style,1,0);if(close!=null)panel.Controls.Add(close,1,1);
   var button=new Button{Name="pet001_test_button",Text="EM FASE DE TESTE",Dock=DockStyle.Fill,Margin=new Padding(3),AutoSize=false,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(25,85,155),ForeColor=Color.White,Font=new Font("Segoe UI",8,FontStyle.Bold),Cursor=Cursors.Hand,AccessibleName="EM FASE DE TESTE - PET LOJA LEAL INFO V1.2"};
   button.FlatAppearance.BorderColor=Color.Cyan;
   button.Click+=(_,_)=>{
    using var info=new Form{Name="pet001_test_dialog",Text="LOJA LEAL INFO • PET V1.2",StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(400,170),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(16,29,45)};
    info.Controls.Add(new Label{Text="EM FASE DE TESTE\n\nAlteração visual do PET V1.1 para V1.2.\nNenhuma operação de venda é executada.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,Font=new Font("Segoe UI",11)});
    var ok=new Button{Text="FECHAR",Dock=DockStyle.Bottom,Height=36,DialogResult=DialogResult.OK};info.Controls.Add(ok);info.AcceptButton=ok;info.CancelButton=ok;info.ShowDialog(form);
   };
   panel.Controls.Add(button,0,0);
  }finally{panel.ResumeLayout(true);}
 }
 static IEnumerable<Control> Walk(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
}
