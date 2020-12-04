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
}