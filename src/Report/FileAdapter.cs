namespace SerialportCli.Report;

public class FileAdapter(Uri url) : IReportAdapter
{
    public void Append(string name, long totalRecv, long totalSend, long totalRecvError, long totalSendError, long elapsed)
    {
        string path = url.LocalPath.StartsWith(@"\\") switch
        {
            true => url.LocalPath.Substring(2),
            false => url.LocalPath
        };

        if (!File.Exists(path))
        {
            File.WriteAllLines(path, [
                """
                "Name","RX(byte)","TX(byte)","RXError","TXError","Elapsed(ms)"
                """
            ]);
        }

        while (true)
        {
            try
            {
                using var fs = File.Open(path, FileMode.Append);
                using var sw = new StreamWriter(fs);
                sw.WriteLine($"\"{name}\",\"{totalRecv}\",\"{totalSend}\",\"{totalRecvError}\",\"{totalSendError}\",\"{elapsed}\"");
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(1);
            }
        }
    }
}