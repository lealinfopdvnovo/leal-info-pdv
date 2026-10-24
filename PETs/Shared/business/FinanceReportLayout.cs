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
  if(layout.GetControlFromPosition(0,2) is TableLayoutPanel indicators){
   var colors=new[]{Color.FromArgb(0,125,185),Color.FromArgb(94,102,110),Color.FromArgb(0,145,92),Color.FromArgb(190,58,55),Color.FromArgb(95,72,175),Color.FromArgb(222,126,20)};
   for(int i=0;i<6;i++)if(indicators.GetControlFromPosition(i,0) is Label card){
    int index=i;
    void Restore(){var color=index==4&&card.Text.Contains('-')?Color.FromArgb(180,45,45):colors[index];if(card.BackColor!=color)card.BackColor=color;card.ForeColor=Color.White;}
    card.BackColorChanged+=(_,_)=>Restore();card.TextChanged+=(_,_)=>Restore();Restore();
   }
  }
  // Preferred height includes scaled padding, margins and actual button height.
  // A fixed row can clip the controls after Windows font/DPI autoscaling.
  footer.WrapContents=false;footer.AutoSize=true;footer.AutoSizeMode=AutoSizeMode.GrowAndShrink;
  layout.RowStyles[5].SizeType=SizeType.AutoSize;
  if(layout.GetControlFromPosition(0,4) is Label status){status.AutoEllipsis=true;}
  foreach(var grid in Walk(form).OfType<DataGridView>().ToArray()){
   void Columns(){
    foreach(DataGridViewColumn c in grid.Columns){
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

