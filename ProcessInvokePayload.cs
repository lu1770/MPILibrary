namespace MPILibrary;

public class ProcessInvokePayload
{
    public string Type { get; set; }
    public string Method { get; set; }
    public object Parameters { get; set; }
}