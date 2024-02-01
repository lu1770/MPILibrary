using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using Newtonsoft.Json;

namespace MPILibrary;

public static class ProcessParallel
{
    public static void Handle()
    {
        ProcessInvoke(ArgsToDictionary(Environment.GetCommandLineArgs()));
        Environment.Exit(0);
    }

    public static Dictionary<string, string> ArgsToDictionary(string[] args)
    {
        var dict = new Dictionary<string, string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("-") || args[i].StartsWith("--"))
            {
                // Key is current arg without "-"
                var key = args[i].TrimStart('-', '/');

                if (i == args.Length - 1 || args[i + 1].StartsWith("-") || args[i + 1].StartsWith("--"))
                {
                    dict.Add(key, "");
                }
                else
                {
                    dict.Add(key, args[i + 1]);
                    i++; // Skip the value part
                }
            }
        }

        return dict;
    }

    public static void ProcessInvoke(Dictionary<string, string> dictionary)
    {
        try
        {
            var value = dictionary["args"];
            var payload = JsonConvert.DeserializeObject<ProcessInvokePayload>(value);
            var type = Type.GetType(payload.Type);
            var methodInfo = type.GetMethod(payload.Method);
            var result = methodInfo.Invoke(null, new[] { JsonConvert.DeserializeObject(payload.Parameters, methodInfo.GetParameters().Select(p=>p.ParameterType).FirstOrDefault()) });
            WriteOutput(result);
        }
        catch (Exception e)
        {
            WriteOutput(new ExceptionMessage
            {
                Message = e.Message,
                StackTrace = e.StackTrace
            });
        }
    }

    private static void WriteOutput(object target)
    {
        var json = JsonConvert.SerializeObject(JsonConvert.SerializeObject(target));
        Console.WriteLine(new StringBuilder().Append(SEP).Append(json));
    }

    private static readonly string SEP = string.Concat(Enumerable.Repeat('|', 30));


    private static BlockingCollection<string> ProcessPool = new(Environment.ProcessorCount);
    private static int _maxProcessLimit = Environment.ProcessorCount;

    public static int MaxProcessLimit
    {
        get => _maxProcessLimit;
        set => ProcessPool = new(_maxProcessLimit = value);
    }

    public static bool EnableDebug { get; set; }

    public static IList<TOut> ForEach<TIn, TOut>(IEnumerable<TIn> items, Func<TIn, TOut> fn) where TIn : notnull
    {
        if (items == null || fn == null)
        {
            return new List<TOut>();
        }
        
        var methodInfo = fn.Method;
        var methodName = methodInfo.Name;
        if (!methodInfo.IsStatic)
        {
            throw new InvalidOperationException($"Unable to invoke a non-static method {methodName}");
        }
        var typeName = methodInfo.DeclaringType.AssemblyQualifiedName;
        var method = Type.GetType(typeName).GetMethod(methodName);
        if (method == null)
        {
            throw new MissingMethodException(typeName, methodName);
        }

        ConcurrentQueue<TOut> queue = new ConcurrentQueue<TOut>();
        var count = items.Count();
        var batchCount = count / MaxProcessLimit + 1;
        ParallelOptions parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = -1
        };
        for (var i = 0; i < batchCount; i++)
        {
            var batch = items.Skip(i * MaxProcessLimit).Take(MaxProcessLimit).ToList();
            Debug.WriteLine($"Batch {i + 1}/{batchCount} batch size is {batch.Count}");
            Parallel.ForEach(batch, parallelOptions, item =>
            {
                var output = CreateProcess<TIn, TOut>(typeName, methodName, item);
                queue.Enqueue(output);
            });
        }

        if (queue.Count != count)
        {
            throw new Exception($"Result mismatched {queue.Count}/{count}");
        }

        return queue.ToList();
    }

    private static TOut? CreateProcess<TIn, TOut>(string typeName, string methodName, TIn item)
    {
        var args = GetSubProcessArgs(new ProcessInvokePayload
        {
            Type = typeName,
            Method = methodName,
            Parameters = JsonConvert.SerializeObject(item),
        });
        var executionFileName = GetExecutionFileName();
        CheckEntryPointThrow(executionFileName);
        var originalCommandLineArgs = GetOriginalCommandLineArgs();
        var startInfo = new ProcessStartInfo(executionFileName, $"--args {args} {originalCommandLineArgs}")
        {
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        var process = Process.Start(startInfo);
        var readToEnd = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (string.IsNullOrEmpty(readToEnd))
        {
            return default;
        }

        if (!readToEnd.Contains(SEP))
        {
            throw new Exception($"Internal Error: Invalid output format [{readToEnd}]");
        }
#if DEBUG
        if (EnableDebug)
        {
            Debug.WriteLine(new StringBuilder().Append("Pid:").AppendLine(process.Id.ToString()).AppendLine(readToEnd));
        }
#endif
        var jsonWrappedText = readToEnd.Split(SEP).Last();
        var jsonText = JsonConvert.DeserializeObject<string>(jsonWrappedText);
        if (jsonText.Contains($"\"{nameof(ExceptionMessage.StackTrace)}\""))
        {
            var error = JsonConvert.DeserializeObject<ExceptionMessage>(jsonText);
            Console.WriteLine(new StringBuilder().Append("Error:").AppendLine(error.Message).Append(nameof(ExceptionMessage.StackTrace)).AppendLine(error.StackTrace).ToString());
            throw new Exception(error.Message);
        }
        var output = JsonConvert.DeserializeObject<TOut>(jsonText);
        return output;
    }

    private static string GetOriginalCommandLineArgs()
    {
        return string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
    }

    private static string GetSubProcessArgs(ProcessInvokePayload processInvokePayload)
    {
        var serializeObject = JsonConvert.SerializeObject(processInvokePayload);
        var args = JsonConvert.SerializeObject(serializeObject);
        return args;
    }

    private static string? GetExecutionFileName()
    {
        return Process.GetCurrentProcess().MainModule?.FileName;
    }

    private static void CheckEntryPointThrow(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
        {
            throw new Exception($"Current process must have a Main method as entry point");
        }
    }

    private static ProcessStartInfo GetDotNetStartInfo(string commandLine, string args)
    {
        return new ProcessStartInfo("dotnet", $"exec {commandLine} --args {args}")
        {
            RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8
        };
    }

    public static bool IsSubProcess()
    {
        return ArgsToDictionary(Environment.GetCommandLineArgs()).ContainsKey("args");
    }
}