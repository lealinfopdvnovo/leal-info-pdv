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
    private readonly Label total=new(){Name="KitchenTableTotal",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=KitchenPremiumStyle.Gold,Font=new Font("Segoe UI",11,FontStyle.Bold)};
    private readonly Button addToTable=new(){Name="DashboardAddToTable",Text="ADICIONAR À MESA",Dock=DockStyle.Fill};
    private readonly Button payment=new(){Name="KitchenReceivePayment",Text="RECEBER / PAGAMENTO",Dock=DockStyle.Fill,Height=44};
    private readonly TableLayoutPanel header=new(){Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};
    private DateTimeOffset? started;
    private bool persisted;
    private readonly System.Windows.Forms.Timer clock=new(){Interval=1000};
    internal event Action? AddToTableRequested,SubmitRequested,PrintRequested,ReadyRequested,ClearRequested,PaymentRequested;
    internal event Action<int>? EditRequested,RemoveRequested;
    internal event Action<string>? StatusRequested;
    internal event Action<Guid,int,string>? ItemStatusRequested;
    internal KitchenOrderPanel()
    {
        Name="KitchenDashboardOrder";Dock=DockStyle.Fill;MinimumSize=new Size(0,284);Padding=new Padding(10);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=5,ColumnCount=1,Margin=Padding.Empty};
        root.SizeChanged+=(_,_)=>{if(root.RowStyles.Count==5){bool compact=root.Height<220;root.RowStyles[0].Height=compact?42:48;root.RowStyles[3].Height=compact?58:72;}};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,30));root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));header.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        header.Controls.Add(title,0,0);header.Controls.Add(elapsed,1,0);header.Controls.Add(clear,2,0);root.Controls.Add(header,0,0);
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="Quantity",HeaderText="QTD",FillWeight=13,MinimumWidth=42});
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="Product",HeaderText="PRODUTO",FillWeight=44});
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="Notes",HeaderText="OBSERVAÇÃO",FillWeight=43});
        Rows.Columns.Add(new DataGridViewButtonColumn{Name="Edit",HeaderText="",Text="✎",UseColumnTextForButtonValue=true,Width=36,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});
        Rows.Columns.Add(new DataGridViewButtonColumn{Name="Remove",HeaderText="",Text="×",UseColumnTextForButtonValue=true,Width=36,AutoSizeMode=DataGridViewAutoSizeColumnMode.None});
        Rows.Columns.Add(new DataGridViewButtonColumn{Name="StatusMenu",HeaderText="",Text="▼",ToolTipText="Alterar status do pedido",UseColumnTextForButtonValue=true,Width=32,MinimumWidth=32,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,DisplayIndex=2});
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="UnitPrice",HeaderText="UNIT. R$",FillWeight=18,DefaultCellStyle=new DataGridViewCellStyle{Format="N2"}});
        Rows.Columns.Add(new DataGridViewTextBoxColumn{Name="LineTotal",HeaderText="TOTAL R$",FillWeight=19,DefaultCellStyle=new DataGridViewCellStyle{Format="N2"}});
        Rows.RowTemplate.Height=34;Rows.ColumnHeadersHeight=30;Rows.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        Rows.CellPainting+=(_,e)=>
        {
            if(e.RowIndex<0||e.ColumnIndex<3||e.ColumnIndex>5)return;
            using var background=new SolidBrush(Color.FromArgb(7,13,19));e.Graphics!.FillRectangle(background,e.CellBounds);
            var box=Rectangle.Inflate(e.CellBounds,-4,-3);var accent=e.ColumnIndex==4?Color.FromArgb(255,65,65):Color.FromArgb(115,174,207);
            KitchenLighting.Surface(e.Graphics,box,accent,false);
            KitchenPremiumStyle.DrawFittedText(e.Graphics,e.ColumnIndex==5?"▼":e.ColumnIndex==4?"×":"✎",box,Color.White,17);e.Handled=true;
        };
        var states=new ContextMenuStrip{Name="KitchenOrderStatusMenu",ShowImageMargin=false,BackColor=KitchenPremiumStyle.Surface,ForeColor=Color.White};
        foreach(var pair in new[]{("Preparando","PREPARANDO"),("Pronto","PRONTO"),("Entregue","ENTREGUE"),("Cancelado","CANCELADO")}){var item=states.Items.Add(pair.Item1);item.Tag=pair.Item2;item.Click+=(_,_)=>{if(!persisted)return;if(Rows.CurrentRow?.Tag is KitchenTableLine line)ItemStatusRequested?.Invoke(line.OrderId,line.Position,pair.Item2);else StatusRequested?.Invoke(pair.Item2);};}
        states.Opening+=(_,e)=>{e.Cancel=!persisted;if(Rows.CurrentRow?.Tag is KitchenTableLine line)foreach(ToolStripItem item in states.Items){string target=Convert.ToString(item.Tag)??"";item.Enabled=target=="CANCELADO"||!line.TableOnly&&((line.Status,target) is ("AGUARDANDO","PREPARANDO") or ("AGUARDANDO","PRONTO") or ("PREPARANDO","PRONTO") or ("PRONTO","ENTREGUE"));}};
        Rows.CellMouseDown+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0)Rows.CurrentCell=Rows.Rows[e.RowIndex].Cells[e.ColumnIndex];};
        Rows.CellContentClick+=(_,e)=>{
            if(e.RowIndex<0)return;
            if(persisted){if(e.ColumnIndex==Rows.Columns["StatusMenu"].Index){var cell=Rows.GetCellDisplayRectangle(e.ColumnIndex,e.RowIndex,true);states.Show(Rows,new Point(cell.Left,cell.Bottom));}return;}
            if(e.ColumnIndex==3)EditRequested?.Invoke(e.RowIndex);if(e.ColumnIndex==4)RemoveRequested?.Invoke(e.RowIndex);
        };
        Rows.ContextMenuStrip=states;Rows.Disposed+=(_,_)=>states.Dispose();
        root.Controls.Add(Rows,0,1);
        var actions=new TableLayoutPanel{Name="DashboardHeroActions",Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,8,0,0)};
        for(int i=0;i<3;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        actions.Controls.Add(send,0,0);actions.Controls.Add(print,1,0);actions.Controls.Add(ready,2,0);root.Controls.Add(total,0,2);root.Controls.Add(actions,0,3);
        var billing=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};billing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));billing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));billing.Controls.Add(addToTable,0,0);billing.Controls.Add(payment,1,0);root.Controls.Add(billing,0,4);Controls.Add(root);
        addToTable.Click+=(_,_)=>AddToTableRequested?.Invoke();
        payment.Click+=(_,_)=>PaymentRequested?.Invoke();
        clear.Click+=(_,_)=>ClearRequested?.Invoke();send.Click+=(_,_)=>SubmitRequested?.Invoke();print.Click+=(_,_)=>PrintRequested?.Invoke();ready.Click+=(_,_)=>ReadyRequested?.Invoke();
        KitchenPremiumStyle.Apply(this);KitchenPremiumStyle.Frame(this);
        elapsed.ForeColor=Color.OrangeRed;Rows.ColumnHeadersDefaultCellStyle.ForeColor=Color.Silver;Rows.ColumnHeadersDefaultCellStyle.SelectionBackColor=KitchenPremiumStyle.Surface;Rows.ColumnHeadersDefaultCellStyle.SelectionForeColor=Color.Silver;
        Rows.DefaultCellStyle.BackColor=Color.FromArgb(7,13,19);Rows.RowsDefaultCellStyle.BackColor=Color.FromArgb(7,13,19);Rows.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(13,21,29);Rows.DefaultCellStyle.ForeColor=Color.White;Rows.RowsDefaultCellStyle.ForeColor=Color.White;Rows.DefaultCellStyle.SelectionBackColor=Color.FromArgb(23,53,64);Rows.DefaultCellStyle.Padding=new Padding(5,2,5,2);
        clock.Tick+=(_,_)=>Tick();clock.Start();Display("",Array.Empty<KitchenItem>(),false,null,false,"");
    }
    internal void AttachNewOrder(Button button){button.Dock=DockStyle.Fill;button.Margin=new Padding(2);header.Controls.Add(button,3,0);}
    internal void Display(string reference,KitchenItem[] items,bool saved,DateTimeOffset? since,bool canSend,string status,IReadOnlyList<KitchenTableLine>? lines=null)
    {
        if(IsDisposed||Disposing)return;
        if(InvokeRequired){if(IsHandleCreated)BeginInvoke((Action)(()=>Display(reference,items,saved,since,canSend,status,lines)));return;}
        persisted=saved;started=lines!=null?lines.Where(l=>!l.TableOnly&&l.Status is ("AGUARDANDO" or "PREPARANDO" or "PRONTO")).Any()?since:null:since;payment.Enabled=saved&&items.Length>0;addToTable.Enabled=!saved&&canSend&&items.Length>0;title.Text=(reference.StartsWith("Mesa ",StringComparison.OrdinalIgnoreCase)?"PEDIDO DA "+reference.ToUpperInvariant():string.IsNullOrWhiteSpace(reference)?"PEDIDO DA MESA":"PEDIDO • "+reference)+(saved?"   •   "+status:"");
        Rows.Rows.Clear();decimal sum=0;
        for(int i=0;i<items.Length;i++){var item=items[i];int index=Rows.Rows.Add(item.Quantity.ToString("0.###",CultureInfo.GetCultureInfo("pt-BR")),item.Description,item.Notes);var row=Rows.Rows[index];row.Cells["UnitPrice"].Value=item.UnitPrice;decimal value=decimal.Round(item.Quantity*(item.UnitPrice??0),2,MidpointRounding.AwayFromZero);row.Cells["LineTotal"].Value=value;sum+=value;if(lines!=null&&i<lines.Count)row.Tag=lines[i];}
        total.Text="CONTA DA MESA: "+sum.ToString("C2",CultureInfo.GetCultureInfo("pt-BR"));
        Rows.Columns["StatusMenu"].Visible=saved;Rows.Columns["Edit"].Visible=Rows.Columns["Remove"].Visible=!saved;clear.Enabled=!saved&&canSend&&items.Length>0;send.Enabled=!saved&&canSend&&items.Length>0;print.Enabled=items.Length>0;ready.Enabled=saved&&status is ("AGUARDANDO" or "PREPARANDO");Tick();
    }
    private void Tick(){var age=started.HasValue?DateTimeOffset.UtcNow-started.Value:TimeSpan.Zero;elapsed.Text=(!started.HasValue?"":"TEMPO: ")+((int)Math.Max(0,age.TotalMinutes)).ToString("00")+":"+Math.Max(0,age.Seconds).ToString("00");if(!started.HasValue)elapsed.Text="";}
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
        KitchenLighting.Surface(g,box,accent,Enabled);
        if(string.IsNullOrEmpty(subtitle))
        {
            using var headerIcon=new Font("Segoe MDL2 Assets",Math.Min(30,Height*.40f),GraphicsUnit.Pixel);using var headerInk=new SolidBrush(Enabled?accent:Color.Gray);
            var measured=g.MeasureString(glyph,headerIcon);g.DrawString(glyph,headerIcon,headerInk,new PointF((Width-measured.Width)/2,7));
            KitchenPremiumStyle.DrawFittedText(g,Text,new Rectangle(7,Height/2,Width-14,Math.Max(1,Height/2-8)),Color.White,12*g.DpiY/96f,false);return;
        }
        int icon=Math.Min(38,Width/5);using var font=new Font("Segoe MDL2 Assets",Math.Max(12,icon*.65f),GraphicsUnit.Pixel);using var ink=new SolidBrush(Enabled?accent:Color.Gray);g.DrawString(glyph,font,ink,new PointF(10,(Height-font.GetHeight(g))/2));
        var text=new Rectangle(icon+8,5,Math.Max(1,Width-icon-20),Math.Max(1,Height*55/100));KitchenPremiumStyle.DrawFittedText(g,Text,text,Enabled?Color.White:Color.Gray,15*g.DpiY/96f,true);
        text.Y=Height*53/100;text.Height=Height*35/100;KitchenPremiumStyle.DrawFittedText(g,subtitle,text,Enabled?accent:Color.Gray,11*g.DpiY/96f);
    }
}

internal static class KitchenComanda
{
    internal static string Text(string reference,KitchenItem[] items,string notes)=>"LEAL INFO PDV PRO — COMANDA\n"+reference.ToUpperInvariant()+"\n"+DateTime.Now.ToString("dd/MM/yyyy HH:mm")+"\n\n"+string.Join("\n",items.Select(i=>$"{i.Quantity:0.###} × {i.Description}"+(string.IsNullOrWhiteSpace(i.Notes)?"":"\n   "+i.Notes)))+(string.IsNullOrWhiteSpace(notes)?"":"\n\nOBSERVAÇÕES: "+notes);
    private static string Setting(string key,string fallback="")
    {
        using var db=Database.Open();using var q=db.CreateCommand();q.CommandText="SELECT value FROM settings WHERE key=$k";q.Parameters.AddWithValue("$k",key);return Convert.ToString(q.ExecuteScalar()) is string value&&value.Length>0?value:fallback;
    }
    internal static bool Configure(IWin32Window owner)
    {
        using var dialog=new Form{Name="KitchenPrinterConfiguration",Text="IMPRESSORA DA COMANDA",Size=new Size(580,340),StartPosition=FormStartPosition.CenterParent,MinimumSize=new Size(500,300)};
        var printer=new ComboBox{Name="ComandaPrinter",Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};
        foreach(string name in PrinterSettings.InstalledPrinters)printer.Items.Add(name);
        string saved=Setting("kitchen_printer");printer.SelectedItem=saved;
        if(printer.SelectedIndex<0&&printer.Items.Count>0){using var defaultDocument=new PrintDocument();printer.SelectedItem=defaultDocument.PrinterSettings.PrinterName;if(printer.SelectedIndex<0)printer.SelectedIndex=0;}
        var paper=new ComboBox{Name="ComandaPaper",Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};paper.Items.AddRange(new object[]{"TÉRMICA — 80 mm","TÉRMICA — 58 mm","NÃO TÉRMICA — A4"});paper.SelectedIndex=int.TryParse(Setting("kitchen_paper","0"),out int index)?Math.Clamp(index,0,2):0;
        var save=new Button{Name="SaveComandaPrinter",Text="USAR ESTA IMPRESSORA E FORMATO",Dock=DockStyle.Bottom,Height=52};
        save.Click+=(_,_)=>{
            if(printer.SelectedItem==null){MessageBox.Show(dialog,"Escolha uma impressora instalada no Windows.","Imprimir comanda");return;}
            using var db=Database.Open();using var tx=db.BeginTransaction();
            foreach(var item in new[]{("kitchen_printer",Convert.ToString(printer.SelectedItem)!), ("kitchen_paper",paper.SelectedIndex.ToString())}){using var q=db.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO settings(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";q.Parameters.AddWithValue("$k",item.Item1);q.Parameters.AddWithValue("$v",item.Item2);q.ExecuteNonQuery();}tx.Commit();dialog.DialogResult=DialogResult.OK;
        };
        dialog.Controls.Add(new Label{Text="A comanda será exibida para conferência antes de imprimir. Configure o mesmo papel no driver da impressora.",Dock=DockStyle.Fill,Padding=new Padding(12)});
        dialog.Controls.Add(paper);dialog.Controls.Add(new Label{Text="TIPO E LARGURA DO PAPEL",Dock=DockStyle.Top,Height=30});dialog.Controls.Add(printer);dialog.Controls.Add(new Label{Text="IMPRESSORA INSTALADA",Dock=DockStyle.Top,Height=30});dialog.Controls.Add(save);KitchenPremiumStyle.Apply(dialog);
        return dialog.ShowDialog(owner)==DialogResult.OK;
    }
    internal static void Preview(IWin32Window owner,string text)
    {
        if(PrinterSettings.InstalledPrinters.Count==0){MessageBox.Show(owner,"Selecione uma impressora instalada no Windows para imprimir a comanda.","Imprimir comanda",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        if(!Configure(owner))return;
        using var document=new PrintDocument{DocumentName="Comanda da cozinha"};document.PrinterSettings.PrinterName=Setting("kitchen_printer");
        if(!document.PrinterSettings.IsValid){MessageBox.Show(owner,"A impressora selecionada está indisponível.","Imprimir comanda");return;}
        int mode=int.Parse(Setting("kitchen_paper","0"));bool thermal=mode<2;
        document.DefaultPageSettings.PaperSize=thermal?new PaperSize(mode==0?"Comanda 80 mm":"Comanda 58 mm",mode==0?315:228,1100):new PaperSize("A4",827,1169);
        document.DefaultPageSettings.Landscape=false;document.DefaultPageSettings.Margins=new Margins(thermal?12:40,thermal?12:40,thermal?12:40,thermal?12:40);
        int position=0;document.BeginPrint+=(_,_)=>position=0;
        document.PrintPage+=(_,e)=>{
            var printable=Rectangle.Round(e.PageSettings.PrintableArea);var bounds=Rectangle.Intersect(e.MarginBounds,printable);
            bounds.Offset(-(int)Math.Ceiling(e.PageSettings.HardMarginX),-(int)Math.Ceiling(e.PageSettings.HardMarginY));
            if(bounds.Width>720){bounds.X+=(bounds.Width-720)/2;bounds.Width=720;}
            position=DrawPage(e.Graphics!,bounds,text,position,thermal?9:11);e.HasMorePages=position<text.Length;
        };
        using var preview=new PrintPreviewDialog{Document=document,Width=900,Height=720,StartPosition=FormStartPosition.CenterParent};preview.ShowDialog(owner);
    }
    internal static int DrawPage(Graphics graphics,Rectangle bounds,string text,int start,float fontSize=11)
    {
        using var font=new Font("Segoe UI",fontSize);using var format=new StringFormat(StringFormat.GenericTypographic){FormatFlags=StringFormatFlags.LineLimit};
        graphics.MeasureString(text[start..],font,bounds.Size,format,out int characters,out _);
        if(characters<=0)throw new InvalidOperationException("A área imprimível é insuficiente. Confira papel e margens da impressora.");graphics.DrawString(text.Substring(start,characters),font,Brushes.Black,bounds,format);return start+characters;
    }
}
