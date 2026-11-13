using System.Text.Json;
using LealInfo.Components;

// Engineering-only preflight. This is not a customer updater or an installer.
if(args.Length!=4){Console.Error.WriteLine("Expected isolated origin, candidate package, expected SHA-256 and report outside both inputs");return 2;}
try{
 var origin=Path.GetFullPath(args[0]);var package=Path.GetFullPath(args[1]);var output=Path.GetFullPath(args[3]);
 if(output.StartsWith(origin.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||output==package||File.Exists(output))throw new IOException("Report must be new and outside the installation");
 var plan=InitialMigrationBridge.Prepare(origin,package,args[2]);
 using var report=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(report,plan,new JsonSerializerOptions{WriteIndented=true});report.Flush(true);
 Console.WriteLine("Preflight recorded. No files applied. Core/PET recovery handoff and licensed validation still required.");return 0;
}catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
