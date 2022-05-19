using System;

namespace SerialportCli.Report;

public static class ReportUtils
{
    public static void SaveReport(string reportPath, string name, long totalRecv, long totalSend, System.TimeSpan elapsed)
    {
        var url = new Uri(reportPath);
        IReportAdapter adapter = url.Scheme switch
        {
            "file" => new FileAdapter(url),
            "stdio" => new ConsoleAdapter(),
            "stderr" => new ConsoleAdapter(false),
            _ => new ConsoleAdapter()
        };
        adapter.Append(name, totalRecv, totalSend, elapsed);
    }
}

public interface IReportAdapter
{
    void Append(string name, long totalRecv, long totalSend, System.TimeSpan elapsed);
}

internal class ConsoleAdapter : IReportAdapter
{
    private bool useStdio;

    public ConsoleAdapter(bool useStdio = true)
    {
        this.useStdio = useStdio;
    }

    public void Append(string name, long totalRecv, long totalSend, TimeSpan elapsed)
    {
        if (useStdio)
        {
            Console.WriteLine($"{name},RX:{totalRecv},TX:{totalSend},{elapsed.TotalMilliseconds}");
        }
        else
        {
            Console.Error.WriteLine($"{name},RX:{totalRecv},TX:{totalSend},{elapsed.TotalMilliseconds}");
        }
    }
}