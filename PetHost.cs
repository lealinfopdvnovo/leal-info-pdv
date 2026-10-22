using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("LealInfoPDV.Pet.Profile")]
[assembly: InternalsVisibleTo("LealInfoPDV.Pet.CompatibilityTests")]
namespace LealInfoPDV;
// Generic integration surface: GLOBAL references no client module or domain type.
public sealed partial class MainForm
{
 internal void PetRunAllowed(string permission,Action action)=>RunAllowed(permission,action);
 internal void PetCustomers(Action<long>? select)=>RunAllowed("customers",()=>ShowPersonCrud("CLIENTES","customers",select));
 internal void PetTheme(Form form){ApplyFloatingTheme(form);if(!Application.OpenForms.Cast<Form>().Any(x=>x.Name=="SalesForm"))SalesDialogTheme.Apply(form,GetSetting("sales_theme","Futurista Azul"),null);}
 internal Form PetGrid(string title,string sql,out DataGridView grid)=>GridForm(title,sql,out grid);
 internal Form PetEditor(string title,string[] labels)=>Editor(title,labels);
 internal Button PetAction(string text,Action action)=>ActionButton(text,action);
 internal void PetReload(DataGridView grid,string sql)=>ReloadGrid(grid,sql);
 internal static long? PetSelected(DataGridView grid)=>SelectedId(grid);
 internal static bool PetConfirm(string text)=>Confirm(text);
 internal static void PetInfo(string text)=>Info(text);
 internal static void PetExec(string sql,params (string name,object value)[] pars)=>Exec(sql,pars);
 internal void PetTool(string text,string after,Func<Image> icon,Action action,Color top,Color bottom){
  IEnumerable<Control> Walk(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Walk(x)));
  var caption=Walk(this).FirstOrDefault(x=>x.Text==after);var parent=caption?.Parent?.Parent;if(parent==null)throw new InvalidOperationException("Área de atalhos não encontrada.");
  int index=parent.Controls.GetChildIndex(caption!.Parent!)+1;AddTool(parent,text,"",action,icon,top,bottom);var added=parent.Controls[parent.Controls.Count-1];added.Name="PetModuleTool";parent.Controls.SetChildIndex(added,index);
 }
}
