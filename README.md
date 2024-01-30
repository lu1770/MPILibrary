# MPILibrary a MPI Library for .Net

## MIT License
- [License](LICENSE.md)

## 1. Create a executable program.
## 2. Add sub-processes code on top of Main method.
```csharp
if (ProcessParallel.IsSubProcess())
{
    ProcessParallel.Handle();
    return;
}
```
## 3. Define a static method.
```csharp
class Runner
{
    public static object Run(string id)
    {
        return new
        {
            CommandLineArgs = Environment.GetCommandLineArgs(),
            Message = $"input is {id}, process id is {Process.GetCurrentProcess().Id}",
            Time=DateTime.Now.ToShortTimeString()
        };
    }
}
```
## 4. Invoke multi-process runners
```csharp
// Generate Items
var items = Enumerable.Range(1, 200).Select(i => Guid.NewGuid().ToString());

// Show MaxProcessLimit
Console.WriteLine($"Start {ProcessParallel.MaxProcessLimit}");

// Run
List<object> results = ProcessParallel.ForEach(items, Runner.Run).ToList();

// Show Results
Console.WriteLine(JsonConvert.SerializeObject(results, Formatting.Indented));
```
## 5. Tips
### ProcessParallel.MaxProcessLimit will use CPU cores count to fit your machine.
### Only static method is acceptable.
### Any data in memory is inaccessable in sub-process. You can initiate data for sub-process before ProcessParallel.Handle();
