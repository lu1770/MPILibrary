using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using MPILibrary;
using Newtonsoft.Json;


if (ProcessParallel.IsSubProcess())
{
    ProcessParallel.Handle();
    return;
}

Console.Clear();
Console.WriteLine($"Assembly.GetEntryAssembly().Location:{Assembly.GetEntryAssembly().Location}");
Console.WriteLine($"AppDomain.CurrentDomain.SetupInformation.ApplicationBase:{AppDomain.CurrentDomain.SetupInformation.ApplicationBase}");
Console.WriteLine($"Process.GetCurrentProcess().MainModule.FileName:{Process.GetCurrentProcess().MainModule.FileName}");
Console.WriteLine(string.Join(";",Environment.GetCommandLineArgs()));
var items = Enumerable.Range(1, 200).Select(i => Guid.NewGuid().ToString());
Console.WriteLine($"Start {ProcessParallel.MaxProcessLimit}");
List<object> results = ProcessParallel.ForEach(items, Runner.Run).ToList();
Console.WriteLine(JsonConvert.SerializeObject(results, Formatting.Indented));