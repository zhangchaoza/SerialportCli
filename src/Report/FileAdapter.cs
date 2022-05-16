using System;
using System.IO;
using System.Threading;

namespace SerialportCli.Report;

public class FileAdapter : IReportAdapter
{
    private Uri url;

    public FileAdapter(Uri url)
    {
        this.url = url;
    }

    public void Append(string name, long totalRecv, long totalSend, TimeSpan elapsed)
    {
        string path = "";

        if (url.LocalPath.StartsWith(@"\\"))
        {
            path = url.LocalPath.Substring(2);
        }
        else
        {
            path = url.LocalPath;
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
            catch (System.IO.IOException)
            {
                Thread.Sleep(1);
                continue;
            }
        }
    }
}