using System.Diagnostics;
namespace Leal.Diagnostics;
internal sealed record Machine(string Windows,string Architecture,string Processor,int LogicalProcessors,long TotalRam,long ToolWorkingSet,string Drives);
internal sealed record Sample(double Seconds,string Stage,int? Pid,double? PdvCpu,double? SystemCpu,long? WorkingSet,long? PrivateBytes,long AvailableRam,ulong? ReadBytes,ulong? WriteBytes,int? TcpConnections,long NetworkReceived,long NetworkSent,bool ProcessAvailable,string? Note);
internal sealed record StageMark(double Seconds,string Stage);
internal sealed record FileEntry(string RelativeName,long Bytes,string Extension);
internal sealed record FileGroup(string Extension,int Files,long Bytes);
internal sealed record DuplicateCandidate(long Bytes,string Extension,string[] Files);
internal sealed record Inventory(string? Folder,string? Version,long Files,long Bytes,List<FileGroup> Groups,List<FileEntry> Largest,List<DuplicateCandidate> SameSizeImages,int Skipped,bool Complete,string Note);
internal sealed class Session
{
    public Session(){}
    public string ToolVersion {get;set;}="1.0.0";
    public DateTimeOffset Started {get;set;}=DateTimeOffset.Now;
    public Machine Computer {get;set;}=null!;
    public Inventory Files {get;set;}=new(null,null,0,0,new(),new(),new(),0,false,"Pasta não selecionada.");
    public List<Sample> Samples {get;set;}=new();
    public List<StageMark> Stages {get;set;}=new();
    public List<string> Limitations {get;set;}=new(){"O analisador também consome CPU e memória; o uso total do PC inclui esse custo.","CPU do PDV normalizada pelos processadores lógicos. RAM privada e conjunto de trabalho são medidas diferentes.","E/S do processo inclui cache e comunicação; não equivale a acesso físico ao disco.","Bytes de rede pertencem aos adaptadores ativos do computador e podem contar interfaces virtuais. Não são atribuídos ao PDV nem ao Firebase.","Contagem TCP pertence ao PID monitorado, quando disponível; não identifica serviço, conteúdo ou tráfego em bytes.","Tipo físico SSD/HDD e latência física do disco não são identificados nesta versão.","Não mede automaticamente duração de abertura, latência das telas, sincronização ou vazamento de memória.","Inventário lê metadados. Mesmo tamanho e extensão indicam apenas candidatos a duplicação; nenhum conteúdo de banco, licença ou configuração é lido."};
    public bool Interrupted {get;set;}
}
internal static class Units
{
    internal static string Size(double bytes)=>bytes>=1073741824?$"{bytes/1073741824:N2} GB":$"{bytes/1048576:N1} MB";
    internal static string Metric(double? n,string suffix="%")=>n.HasValue?$"{n:N1}{suffix}":"indisponível";
}
