namespace SerialportCli.IO.Ports;

using System.IO.Ports;

public class SerialConnectInfo
{
    public SerialConnectInfo(string port, int baudRate, Parity parity, int dataBits, StopBits stopBits)
    {
        Port = port;
        BaudRate = baudRate;
        Parity = parity;
        DataBits = dataBits;
        StopBits = stopBits;
    }

    public string Port { get; }

    public int BaudRate { get; }

    public Parity Parity { get; }

    public int DataBits { get; }

    public StopBits StopBits { get; }
}
