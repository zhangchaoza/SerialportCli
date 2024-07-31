namespace SerialportCli.Report;

public class FileAdapter : IReportAdapter
{
    private Uri url;

    public FileAdapter(Uri url)
    {
        this.url = url;
    }

    public void Append(string name, long totalRecv, long totalSend, long elapsed)
    {
        string path = url.LocalPath.StartsWith(@"\\") switch
        {
            true => url.LocalPath.Substring(2),
            false => url.LocalPath
        };

        if (!File.Exists(path))
        {
            File.WriteAllLines(path, ["""
                                      "Name","RX(byte)","TX(byte)","Elapsed(ms)"
                                      """]);
        }

        while (true)
        {
            try
            {
                using var fs = File.Open(path, FileMode.Append);
                using var sw = new StreamWriter(fs);
                sw.WriteLine($"\"{name}\",\"{totalRecv}\",\"{totalSend}\",\"{elapsed}\"");
                break;
            }
            catch (IOException)
            {
                Thread.Sleep(1);
                continue;
            }
        }
    }
}