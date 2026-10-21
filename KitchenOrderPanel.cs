using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
namespace LealInfoPDV;

// Presentation of the existing draft/order only. Sending and status changes stay in KitchenForm/KitchenStore.
internal sealed class KitchenOrderPanel : UserControl
{
    private readonly Label title=new KitchenCenteredLabel(){Name="DashboardOrderTitle",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",12,FontStyle.Bold)};
    private readonly Label elapsed=new KitchenCenteredLabel(){Name="DashboardOrderElapsed",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.OrangeRed};
    private readonly Button clear=new(){Name="DashboardClearDraft",Text="LIMPAR",Dock=DockStyle.Fill};
    internal readonly DataGridView Rows=new(){Name="DashboardOrderItems",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=KitchenPremiumStyle.Background,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false};
    private readonly KitchenHeroButton send=new("DashboardSend","ENVIAR PARA COZINHA","CONFIRMAR PEDIDO",Color.FromArgb(0,226,100),"\uE8F4");
    private readonly KitchenHeroButton print=new("DashboardPrint","IMPRIMIR COMANDA","PEDIDO DA MESA",Color.FromArgb(0,163,255),"\uE749");
    private readonly KitchenHeroButton ready=new("DashboardReady","MARCAR PRONTO","FINALIZAR PREPARO",Color.FromArgb(255,200,40),"\uE7F4");
    private readonly TableLayoutPanel header=new(){Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};
    private DateTimeOffset? started;
    private bool persisted;
    private readonly System.Windows.Forms.Timer clock=new(){Interval=1000};
    internal event Action? SubmitRequested,PrintRequested,ReadyRequested,ClearRequested;
    internal event Action<int>? EditRequested,RemoveRequested;
    internal event Action<string>? StatusRequested;
    internal KitchenOrderPanel()
    {
        Name="KitchenDashboardOrder";Dock=DockStyle.Fill;Padding=new Padding(10);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Margin=Padding.Empty};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));
        
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));header.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        header.Controls.Add(title,0,0);header.Controls.Add(elapsed,1,0);header.Controls.Add(clear,2,0);root.Controls.Add(header,0,0);
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="Quantity",HeaderText="QTD",FillWeight=13,MinimumWidth=42});
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="Product",HeaderText="PRODUTO",FillWeight=44});
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="Notes",HeaderText="OBSERVAÇÃO",FillWeight=43});
        Rows.Columns.Add(new DataGridViewButtonColumn{Name="Edit",HeaderText="",Text="✎",UseColumnTextForButtonValue=true,Width=36,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});
        Rows.Columns.Add(new DataGridViewButtonColumn{Name="Remove",HeaderText="",Text="×",UseColumnTextForButtonValue=true,Width=36,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});
        Rows.RowTemplate.Height=34;Rows.ColumnHeadersHeight=30;Rows.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        Rows.CellContentClick+=(_,e)=>{if(persisted||e.RowIndex<0)return;if(e.ColumnIndex==3)EditRequested?.Invoke(e.RowIndex);if(e.ColumnIndex==4)RemoveRequested?.Invoke(e.RowIndex);};
        var states=new ContextMenuStrip();
        foreach(string status in new[]{"PREPARANDO","PRONTO","ENTREGUE","CANCELADO"}){var item=states.Items.Add(status);item.Click+=(_,_)=>{if(persisted)StatusRequested?.Invoke(status);};}
        Rows.ContextMenuStrip=states;Rows.Disposed+=(_,_)=>states.Dispose();
        root.Controls.Add(Rows,0,1);
        var actions=new TableLayoutPanel{Name="DashboardHeroActions",Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,8,0,0)};
        for(int i=0;i<3;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        actions.Controls.Add(send,0,0);actions.Controls.Add(print,1,0);actions.Controls.Add(ready,2,0);root.Controls.Add(actions,0,2);Controls.Add(root);
        clear.Click+=(_,_)=>ClearRequested?.Invoke();send.Click+=(_,_)=>SubmitRequested?.Invoke();print.Click+=(_,_)=>PrintRequested?.Invoke();ready.Click+=(_,_)=>ReadyRequested?.Invoke();
        KitchenPremiumStyle.Apply(this);KitchenPremiumStyle.Frame(this);
        elapsed.ForeColor=Color.OrangeRed;Rows.ColumnHeadersDefaultCellStyle.ForeColor=Color.Silver;Rows.ColumnHeadersDefaultCellStyle.SelectionBackColor=KitchenPremiumStyle.Surface;Rows.ColumnHeadersDefaultCellStyle.SelectionForeColor=Color.Silver;
        Rows.DefaultCellStyle.SelectionBackColor=Color.FromArgb(49,43,22);Rows.DefaultCellStyle.Padding=new Padding(5,2,5,2);
        clock.Tick+=(_,_)=>Tick();clock.Start();Display("",Array.Empty<KitchenItem>(),false,null,false,"");
    }
    internal void AttachNewOrder(Button button){button.Dock=DockStyle.Fill;button.Margin=new Padding(2);header.Controls.Add(button,3,0);}
    internal void Display(string reference,KitchenItem[] items,bool saved,DateTimeOffset? since,bool canSend,string status)
    {
        persisted=saved;started=since;title.Text=(reference.StartsWith("Mesa ",StringComparison.OrdinalIgnoreCase)?"PEDIDO DA "+reference.ToUpperInvariant():string.IsNullOrWhiteSpace(reference)?"PEDIDO DA MESA":"PEDIDO • "+reference)+(saved?"   •   "+status:"");
        Rows.Rows.Clear();foreach(var item in items)Rows.Rows.Add(item.Quantity.ToString("0.###",CultureInfo.GetCultureInfo("pt-BR")),item.Description,item.Notes);
        Rows.Columns["Edit"].Visible=Rows.Columns["Remove"].Visible=!saved;clear.Enabled=!saved&&canSend&&items.Length>0;send.Enabled=!saved&&canSend&&items.Length>0;print.Enabled=items.Length>0;ready.Enabled=saved&&status is ("AGUARDANDO" or "PREPARANDO");Tick();
    }
    private void Tick(){var age=started.HasValue?DateTimeOffset.UtcNow-started.Value:TimeSpan.Zero;elapsed.Text="TEMPO: "+((int)Math.Max(0,age.TotalMinutes)).ToString("00")+":"+Math.Max(0,age.Seconds).ToString("00");}
    protected override void Dispose(bool disposing){if(disposing)clock.Dispose();base.Dispose(disposing);}
}

internal sealed class KitchenHeroButton : Button
{
    private readonly Color accent;private readonly string subtitle,glyph;
    internal KitchenHeroButton(string name,string text,string subtitle,Color accent,string glyph)
    {Name=name;Text=text;this.subtitle=subtitle;this.accent=accent;this.glyph=glyph;Dock=DockStyle.Fill;Margin=new Padding(4);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;DoubleBuffered=true;Cursor=Cursors.Hand;UseVisualStyleBackColor=false;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(KitchenPremiumStyle.Background);
        var box=new Rectangle(3,3,Math.Max(1,Width-7),Math.Max(1,Height-7));using var shape=KitchenPremiumStyle.Round(box,8);
        using var fill=new LinearGradientBrush(box,Color.FromArgb(accent.R/3,accent.G/3,accent.B/3),Color.FromArgb(3,9,13),90f);g.FillPath(fill,shape);
        using(var glow=new Pen(Color.FromArgb(Enabled?75:20,accent),6))g.DrawPath(glow,shape);using(var edge=new Pen(Enabled?accent:Color.DimGray,1.5f))g.DrawPath(edge,shape);
        if(string.IsNullOrEmpty(subtitle))
        {
            using var headerIcon=new Font("Segoe MDL2 Assets",Math.Min(30,Height*.40f),GraphicsUnit.Pixel);using var headerInk=new SolidBrush(Enabled?accent:Color.Gray);
            var measured=g.MeasureString(glyph,headerIcon);g.DrawString(glyph,headerIcon,headerInk,new PointF((Width-measured.Width)/2,7));
            KitchenPremiumStyle.DrawFittedText(g,Text,new Rectangle(7,Height/2,Width-14,Math.Max(1,Height/2-8)),Color.White,12*g.DpiY/96f,true);return;
        }
        int icon=Math.Min(38,Width/5);using var font=new Font("Segoe MDL2 Assets",Math.Max(12,icon*.65f),GraphicsUnit.Pixel);using var ink=new SolidBrush(Enabled?accent:Color.Gray);g.DrawString(glyph,font,ink,new PointF(10,(Height-font.GetHeight(g))/2));
        var text=new Rectangle(icon+8,5,Math.Max(1,Width-icon-20),Math.Max(1,Height*55/100));KitchenPremiumStyle.DrawFittedText(g,Text,text,Enabled?Color.White:Color.Gray,15*g.DpiY/96f,true);
        text.Y=Height*53/100;text.Height=Height*35/100;KitchenPremiumStyle.DrawFittedText(g,subtitle,text,Enabled?accent:Color.Gray,11*g.DpiY/96f);
    }
}

internal static class KitchenComanda
{
    internal static string Text(string reference,KitchenItem[] items,string notes)=>"LEAL INFO PDV PRO — COMANDA\n"+reference.ToUpperInvariant()+"\n"+DateTime.Now.ToString("dd/MM/yyyy HH:mm")+"\n\n"+string.Join("\n",items.Select(i=>$"{i.Quantity:0.###} × {i.Description}"+(string.IsNullOrWhiteSpace(i.Notes)?"":"\n   "+i.Notes)))+(string.IsNullOrWhiteSpace(notes)?"":"\n\nOBSERVAÇÕES: "+notes);
    internal static void Preview(IWin32Window owner,string text)
    {
        using var document=new PrintDocument{DocumentName="Comanda da cozinha"};
        if(!document.PrinterSettings.IsValid){MessageBox.Show(owner,"Selecione uma impressora instalada no Windows para imprimir a comanda.","Imprimir comanda",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        int position=0;document.BeginPrint+=(_,_)=>position=0;
        document.PrintPage+=(_,e)=>{position=DrawPage(e.Graphics!,e.MarginBounds,text,position);e.HasMorePages=position<text.Length;};
        using var preview=new PrintPreviewDialog{Document=document,Width=900,Height=720,StartPosition=FormStartPosition.CenterParent};preview.ShowDialog(owner);
    }
    internal static int DrawPage(Graphics graphics,Rectangle bounds,string text,int start)
    {
        using var font=new Font("Segoe UI",11);using var format=new StringFormat(StringFormat.GenericTypographic){FormatFlags=StringFormatFlags.LineLimit};
        graphics.MeasureString(text[start..],font,bounds.Size,format,out int characters,out _);
        if(characters<=0)return text.Length;graphics.DrawString(text.Substring(start,characters),font,Brushes.Black,bounds,format);return start+characters;
    }
}
