using System.IO.Ports;

namespace SerialportCli
{
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
        public string Name { get; set; }

        public int Baudrate { get; set; }

        public Parity Parity { get; set; }

        public int Databits { get; set; }

        public StopBits Stopbits { get; set; }

    }
}