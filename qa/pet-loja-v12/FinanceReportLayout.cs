using System.Runtime.CompilerServices;
internal static class FinanceReportLayout
{
 static readonly ConditionalWeakTable<Form,object> Applied=new();
 internal static void Apply(Form form)
 {
  if(Applied.TryGetValue(form,out _))return;
  var layout=form.Controls.OfType<TableLayoutPanel>().SingleOrDefault();
  if(layout==null||layout.RowCount!=6)return;
  var footer=layout.GetControlFromPosition(0,5) as FlowLayoutPanel;
  if(footer==null)return;
  Applied.Add(form,new object());
  // Preferred height includes scaled padding, margins and actual button height.
  // A fixed row can clip the controls after Windows font/DPI autoscaling.
  footer.WrapContents=false;footer.AutoSize=true;footer.AutoSizeMode=AutoSizeMode.GrowAndShrink;
  layout.RowStyles[5].SizeType=SizeType.AutoSize;
  if(layout.GetControlFromPosition(0,4) is Label status){status.AutoEllipsis=true;}
  foreach(var grid in Walk(form).OfType<DataGridView>()){
   void Columns(){
    foreach(DataGridViewColumn c in grid.Columns){
     c.MinimumWidth=c.Name switch {"Código" or "Venda"=>60,"Data"=>160,"Tipo"=>90,"Descrição" or "Produto"=>220,_=>100};
     c.FillWeight=c.Name switch {"Descrição" or "Produto"=>300,"Data"=>160,"Código" or "Venda"=>65,_=>100};
    }
    grid.ShowCellToolTips=true;
   }
   Columns();grid.DataBindingComplete+=(_,_)=>Columns();
  }
  void Compact(){
   if(layout.GetControlFromPosition(0,0) is Control header)
    layout.RowStyles[0].Height=Math.Max(42,Walk(header).OfType<Label>().Select(l=>l.Font.Height+18).DefaultIfEmpty(42).Max());
   if(layout.GetControlFromPosition(0,2) is Control cards)
    layout.RowStyles[2].Height=Math.Max(64,Walk(cards).OfType<Label>().Select(l=>l.Font.Height*2+24).DefaultIfEmpty(64).Max());
  }
  form.Resize+=(_,_)=>Compact();Compact();form.PerformLayout();
 }
 static IEnumerable<Control> Walk(Control root){foreach(Control c in root.Controls){yield return c;foreach(var n in Walk(c))yield return n;}}
}
