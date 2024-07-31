using System.IO.Ports;

namespace SerialportCli;

internal enum LogLevel
{
    Quiet,

    Minimal,

    Normal,

    Detailed,

    Diagnostic
}

internal class GlobalParams
{
    public LogLevel Verbose { get; set; }

    public bool NoAnsi { get; set; }
}

internal class SerialParams
{
    public const int DEFAULT_BAUDRATE = 9600;
    public const Parity DEFAULT_PARITY = Parity.None;
    public const int DEFAULT_DATABITS = 8;
    public const StopBits DEFAULT_STOPBITS = StopBits.One;

    public SerialParams(
        string port,
        int baudRate = DEFAULT_BAUDRATE,
        Parity parity = DEFAULT_PARITY,
        int dataBits = DEFAULT_DATABITS,
        StopBits stopBits = DEFAULT_STOPBITS)
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