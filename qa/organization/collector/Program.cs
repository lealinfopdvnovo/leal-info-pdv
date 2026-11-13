using LealInfo.Inventory;
ApplicationConfiguration.Initialize();
using var form=new Form{Text="LEAL INFO — Conferir instalação",Width=650,Height=280,StartPosition=FormStartPosition.CenterScreen};
var text=new Label{Dock=DockStyle.Top,Height=140,Padding=new Padding(18),Text="Conferência somente de leitura.\n\nSelecione a pasta do PDV e salve o relatório de versões e hashes.\nNão copia banco, licença, senhas nem conteúdo de arquivos.\nNão altera o PDV e não envia nada pela internet."};
var button=new Button{Text="SELECIONAR PASTA E SALVAR RELATÓRIO",Dock=DockStyle.Bottom,Height=60};
form.Controls.Add(text);form.Controls.Add(button);
button.Click+=async(_,_)=>{
 using var choose=new FolderBrowserDialog{Description="Selecione a pasta que contém LealInfoPDV.exe",SelectedPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"LEAL INFO PDV")};
 if(choose.ShowDialog(form)!=DialogResult.OK)return;
 button.Enabled=false;
 try{
  var report=await Task.Run(()=>InstallationInventory.Collect(choose.SelectedPath));
  if(!report.Files.Any(x=>x.Name.Equals("LealInfoPDV.dll",StringComparison.OrdinalIgnoreCase)))throw new IOException("LealInfoPDV.dll não encontrado. Selecione a pasta correta.");
  using var save=new SaveFileDialog{Filter="Relatório de conferência (*.json)|*.json",DefaultExt="json",AddExtension=true,FileName="LEAL_INFO_CONFERENCIA_INSTALACAO.json"};
  if(save.ShowDialog(form)!=DialogResult.OK)return;
  if(Path.GetFullPath(save.FileName).StartsWith(Path.GetFullPath(choose.SelectedPath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Salve o relatório fora da pasta do PDV.");
  await File.WriteAllTextAsync(save.FileName,InstallationInventory.Serialize(report));
  MessageBox.Show(form,"Relatório salvo. Envie somente esse arquivo JSON para conferirmos a origem da instalação.");
 }catch(Exception e){MessageBox.Show(form,e.Message,"Não foi possível conferir");}finally{button.Enabled=true;}
};
Application.Run(form);
