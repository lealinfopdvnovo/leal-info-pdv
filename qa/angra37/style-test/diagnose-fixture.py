import pathlib
p=pathlib.Path('MainForm.cs')
s=p.read_text(encoding='utf-8-sig');i=s.index('private void OpenSales()');j=s.index('private void AddSaleLabel',i)
part=s[i:j]
part=part.replace('ApplySalesTheme(GetSetting("sales_theme", "Futurista Azul"));','Console.WriteLine("BUILD apply theme");ApplySalesTheme(GetSetting("sales_theme", "Futurista Azul"));Console.WriteLine("BUILD theme applied");')
part=part.replace('ApplyReferenceSalesLayout();','Console.WriteLine("BUILD reference layout");ApplyReferenceSalesLayout();Console.WriteLine("BUILD reference ready");')
part=part.replace('f.ShowDialog(this);','Console.WriteLine("BUILD show sales dialog");f.ShowDialog(this);Console.WriteLine("BUILD sales dialog closed");')
p.write_text(s[:i]+part+s[j:],encoding='utf-8')
