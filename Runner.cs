using System.Diagnostics;

class Runner
{
    public static object Run(string id)
    {
        // Thread.Sleep(3000);
        return new
        {
            CommandLineArgs = Environment.GetCommandLineArgs(),
            Message = $"input is {id}, process id is {Process.GetCurrentProcess().Id}",
            Time=DateTime.Now.ToShortTimeString()
        };
    }
}