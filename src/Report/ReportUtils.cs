namespace SerialportCli.Report;

public static class ReportUtils
{
    public static void SaveReport(string reportPath, string name, long totalRecv, long totalSend, long totalRecvError, long totalSendError, long elapsed)
    {
        var url = new Uri(reportPath);
        IReportAdapter adapter = url.Scheme switch
        {
            "file" => new FileAdapter(url),
            "stdio" => new ConsoleAdapter(),
            "stderr" => new ConsoleAdapter(false),
            _ => new ConsoleAdapter()
        };
        adapter.Append(name, totalRecv, totalSend, totalRecvError, totalSendError, elapsed);
    }
}

public interface IReportAdapter
{
    void Append(string name, long totalRecv, long totalSend, long totalRecvError, long totalSendError, long elapsed);
}

internal class ConsoleAdapter : IReportAdapter
{
    private bool useStdio;

    public ConsoleAdapter(bool useStdio = true)
    {
        this.useStdio = useStdio;
    }

    public void Append(string name, long totalRecv, long totalSend, long totalRecvError, long totalSendError, long elapsed)
    {
        var content = $"{name},RX:{totalRecv},TX:{totalSend},RXError:{totalRecvError},TXError:{totalSendError},{elapsed}";
        (useStdio ? Console.Out : Console.Error).WriteLine(content);
    }
}