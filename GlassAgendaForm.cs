namespace LealInfoPDV;
internal sealed class GlassAgendaForm : Form
{
    private readonly GlassStoredProject? current;
    private readonly DataGridView grid=new(){Name="GlassAgendaGrid",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoGenerateColumns=false};
    private readonly CheckBox history=new(){Name="GlassAgendaHistory",Text="INCLUIR COMPROMISSOS PASSADOS",AutoSize=true};
    internal string? SelectedProjectId{get;private set;}
    internal GlassAgendaForm(GlassStoredProject? source,Action<Form>? theme)
    {
        current=source;Text="AGENDA — VIDRAÇARIA";Name="GlassAgenda";StartPosition=FormStartPosition.CenterParent;ClientSize=new(1080,650);MinimumSize=new(600,420);AutoScaleMode=AutoScaleMode.Dpi;ShowInTaskbar=false;
        foreach(var c in new[]{("At","DATA/HORA"),("Kind","TIPO"),("Customer","CLIENTE"),("Phone","TELEFONE"),("Address","ENDEREÇO"),("Project","PROJETO"),("Notes","OBSERVAÇÃO")})grid.Columns.Add(new DataGridViewTextBoxColumn{DataPropertyName=c.Item1,Name=c.Item1,HeaderText=c.Item2,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});
        grid.Columns["At"].DefaultCellStyle.Format="dd/MM/yyyy HH:mm";grid.Columns["At"].DefaultCellStyle.FormatProvider=GlassBrazilianMoney.Culture;
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new(10)};
        var create=new SalesVisualButton{Name="NewGlassAppointment",Text="AGENDAR MEDIÇÃO / INSTALAÇÃO",AutoSize=true,MinimumSize=new(270,42)};
        create.Enabled=source!=null;
        create.Click+=(_,_)=>{CreateAppointment(theme);Reload();};
        var open=new SalesVisualButton{Name="OpenGlassAgendaProject",Text="ABRIR PROJETO",AutoSize=true,MinimumSize=new(160,42)};
        open.Click+=(_,_)=>{if(grid.CurrentRow?.DataBoundItem is not GlassAppointment appointment)return;SelectedProjectId=appointment.ProjectId;DialogResult=DialogResult.OK;Close();};
        var close=new SalesVisualButton{Text="FECHAR",AutoSize=true,MinimumSize=new(110,42),DialogResult=DialogResult.Cancel};close.Click+=(_,_)=>Close();CancelButton=close;
        actions.Controls.AddRange(new Control[]{create,open,history,close});history.CheckedChanged+=(_,_)=>Reload();
        Controls.Add(grid);Controls.Add(actions);Controls.Add(new Label{Dock=DockStyle.Top,AutoSize=true,Padding=new(10),Text=source==null?"Compromissos futuros • Abra um projeto salvo sem alterações pendentes para criar agendamentos. Cliente/contato vêm do cadastro original.":$"Compromissos futuros • NOVO usa o projeto salvo: {source.Data.Description}. Cliente/telefone/endereço vêm do cadastro original."});theme?.Invoke(this);Reload();
    }
    private void Reload()=>grid.DataSource=GlassOperationsStore.Appointments(!history.Checked).ToList();
    private void CreateAppointment(Action<Form>? theme)
    {
        if(current==null)return;
        using var editor=new Form{Text="AGENDAR — VIDRAÇARIA",Name="GlassAppointmentEditor",ClientSize=new(600,470),MinimumSize=new(450,400),StartPosition=FormStartPosition.CenterParent,AutoScaleMode=AutoScaleMode.Dpi,ShowInTaskbar=false};
        var type=new ComboBox{Name="GlassAppointmentKind",Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};type.Items.AddRange(new[]{"MEDIÇÃO","INSTALAÇÃO"});type.SelectedIndex=0;
        var date=new DateTimePicker{Name="GlassAppointmentDate",Dock=DockStyle.Top,Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy",Value=DateTime.Today.AddDays(1)};
        var time=new DateTimePicker{Name="GlassAppointmentTime",Dock=DockStyle.Top,Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Value=DateTime.Today.AddHours(9)};
        var notes=new TextBox{Name="GlassAppointmentNotes",Dock=DockStyle.Top,Multiline=true,Height=95,MaxLength=2000};
        var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,AutoScroll=true,Padding=new(16)};
        foreach(var field in new[]{("TIPO",(Control)type),("DATA",date),("HORÁRIO",time),("OBSERVAÇÃO",notes)}){fields.Controls.Add(new Label{Text=field.Item1,AutoSize=true});fields.Controls.Add(field.Item2);}
        var save=new SalesVisualButton{Name="SaveGlassAppointment",Text="SALVAR AGENDAMENTO",Dock=DockStyle.Bottom,Height=48};
        save.Click+=(_,_)=>{try{save.Enabled=false;GlassOperationsStore.Schedule(current,type.Text,date.Value.Date+new TimeSpan(time.Value.Hour,time.Value.Minute,0),notes.Text);editor.DialogResult=DialogResult.OK;editor.Close();}catch(Exception e){save.Enabled=true;MessageBox.Show(editor,e.Message,"Agendamento não salvo",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
        editor.Controls.Add(fields);editor.Controls.Add(save);theme?.Invoke(editor);editor.ShowDialog(this);
    }
}
