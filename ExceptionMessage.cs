public class ExceptionMessage
{
    public string Message { get; set; }
    public string StackTrace { get; set; }
    public ExceptionMessage InnerException { get; set; }
    public string ExceptionType { get; set; }
}

public class SubprocessHandleException : Exception
{
    public SubprocessHandleException(ExceptionMessage error) : base(error.Message)
    {
        this.Error = error;
    }
    public ExceptionMessage Error { get; set; }
}