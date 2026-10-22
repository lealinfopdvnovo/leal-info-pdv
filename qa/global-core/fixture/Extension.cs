using LealInfoPDV;
public sealed class LocalExtension:IPetExtension
{
 public void Customize(Form form)=>form.Controls.Add(new Button{Name="PetLocalAction",Text="FUNÇÃO LOCAL PET",Dock=DockStyle.Bottom});
}
