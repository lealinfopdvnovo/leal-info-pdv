using LealInfoPDV;
using System.Windows.Forms;
using System.Drawing;
static class AlertTests
{
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS ALERT "+name);}
 internal static void Run(AngraSyncService service,string localId)
 {
  Exception? failure=null;var thread=new Thread(()=>{try{
   var journal=service.Notifications!;var original=journal.Unread();Check(original.Any(n=>n.LocalId==localId),"imported project and verified photo produce unread notice");
   Check(original.Single(n=>n.LocalId==localId).Customer=="BOB","customer name resolved using existing linked customer");
   journal.Observe(service.Store!);Check(journal.Unread().Count==original.Count,"repeated import produces no repeated notice");
   var reopened=new AngraProjectNotifications(service.Store!);Check(reopened.Unread().Count==original.Count,"unread survives process journal reload");
   using var host=new Form{ClientSize=new Size(1000,700),Text="Isolated PDV desktop"};var footer=new StatusStrip{Dock=DockStyle.Bottom};host.Controls.Add(footer);using var alert=new AngraProjectAlert(host,service);host.Show();Application.DoEvents();alert.RefreshNotice();
   Check(alert.Visible&&alert.Right<=host.ClientSize.Width&&alert.Bottom<footer.Top,"lower right alert stays above blue status bar");
   host.ClientSize=new Size(900,600);Application.DoEvents();Check(alert.Right<=host.ClientSize.Width&&alert.Bottom<footer.Top,"placement remains visible after resizing");
   using(var foreground=new Form{Text="Isolated foreground sales input",ClientSize=new Size(500,200)}){var input=new TextBox{Text="SALE IN PROGRESS",Dock=DockStyle.Top};foreground.Controls.Add(input);foreground.Show(host);foreground.Activate();input.Focus();Application.DoEvents();for(int i=0;i<5;i++){alert.RefreshNotice();Application.DoEvents();}Check(foreground.ActiveControl==input&&input.Text=="SALE IN PROGRESS","background alert never changes foreground input or activates a dialog");foreground.Close();}
   // Opening is explicit. Close the genuine project viewer after it has fired Shown.
   foreach(var n in journal.Unread().Where(n=>n.LocalId!=localId))journal.MarkViewed(n.LocalId);
   using(var closer=new System.Windows.Forms.Timer{Interval=250}){closer.Tick+=(_,_)=>{foreach(Form f in Application.OpenForms.Cast<Form>().Where(f=>f is AngraProjectForm).ToArray())f.Close();};closer.Start();alert.OpenFirst();closer.Stop();}
   Check(!journal.Unread().Any(n=>n.LocalId==localId),"ABRIR PROJETO opens genuine viewer and marks only opened project viewed");alert.RefreshNotice();Check(!alert.Visible,"no passive repeat after viewing");
   reopened=new AngraProjectNotifications(service.Store!);reopened.Observe(service.Store!);Check(!reopened.Unread().Any(n=>n.LocalId==localId),"viewed state survives restart and reimport");
   host.Close();
  }catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!thread.Join(TimeSpan.FromSeconds(45)))throw new Exception("Alert UI timeout");if(failure!=null)throw new Exception("Alert UI verification failed",failure);
 }
}
