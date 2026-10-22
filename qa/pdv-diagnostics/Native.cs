using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Net.NetworkInformation;
namespace Leal.Diagnostics;
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]internal struct Memory {public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,TotalVirtual,AvailableVirtual,AvailableExtendedVirtual;}
    [StructLayout(LayoutKind.Sequential)]internal struct Io {public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref Memory memory);
    [DllImport("kernel32.dll",SetLastError=true)]internal static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    [DllImport("kernel32.dll",SetLastError=true)]internal static extern bool GetProcessIoCounters(IntPtr process,out Io counters);
    [DllImport("iphlpapi.dll",SetLastError=true)]static extern uint GetExtendedTcpTable(IntPtr table,ref int size,bool order,int family,int tableClass,uint reserved);
    internal static Memory Ram(){var m=new Memory{Length=(uint)Marshal.SizeOf<Memory>()};if(!GlobalMemoryStatusEx(ref m))throw new System.ComponentModel.Win32Exception();return m;}
    internal static string Processor(){using var k=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");return Convert.ToString(k?.GetValue("ProcessorNameString"))?.Trim()??"Modelo indisponível";}
    internal static (long received,long sent) Network()
    {
        long received=0,sent=0;
        foreach(var n in NetworkInterface.GetAllNetworkInterfaces())try{if(n.OperationalStatus!=OperationalStatus.Up||n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)continue;var s=n.GetIPv4Statistics();received+=s.BytesReceived;sent+=s.BytesSent;}catch(NetworkInformationException){}
        return(received,sent);
    }
    internal static int? Tcp(int pid)
    {
        int total=0;
        foreach(int family in new[]{2,23})
        {
            int size=0;uint first=GetExtendedTcpTable(IntPtr.Zero,ref size,false,family,5,0);
            if(first!=122&&first!=0)return null;if(size<4||size>16777216)return null;
            IntPtr data=Marshal.AllocHGlobal(size);
            try
            {
                if(GetExtendedTcpTable(data,ref size,false,family,5,0)!=0)return null;
                int rows=Marshal.ReadInt32(data),stride=family==2?24:56,pidOffset=family==2?20:52,stateOffset=family==2?0:48;
                if(rows<0||(long)rows*stride+4>size)return null;
                for(int i=0;i<rows;i++){int offset=4+i*stride;if(Marshal.ReadInt32(data,offset+pidOffset)==pid&&Marshal.ReadInt32(data,offset+stateOffset)==5)total++;}
            }finally{Marshal.FreeHGlobal(data);}
        }
        return total;
    }
}
