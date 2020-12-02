namespace SerialportCli
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Drawing;
    using System.IO.Ports;
    using Pastel;
    using System.Buffers;
    using System.Threading;
    using System.Linq;
    using System.Text;

    internal static class ReplCommand
    {

        private static long totalRecv;
        private static long totalSend;

        public static Command Build()
        {
            var command = new Command("repl", "read and write data in repl mode.");
            command.AddArgument(new Argument<string>("name", description: "name of serial port"));
            command.AddOption(new Option<int>(new string[] { "--baudrate", "-b" }, description: "baudrate of serial port", getDefaultValue: () => 9600));
            command.AddOption(new Option<Parity>(
                new string[] { "--parity", "-p" },
                description: "Parity of serial port.",
                parseArgument: r =>
                {
                    if (r.Tokens.Any())
                    {
                        return Enum.GetValues<Parity>().First(i => Enum.GetName<Parity>(i).StartsWith(r.Tokens[0].Value, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        return Parity.None;
                    }
                },
                isDefault: true));
            command.AddOption(new Option<int>(new string[] { "--databits", "-d" }, description: "databits of serial port", getDefaultValue: () => 8));
            command.AddOption(new Option<StopBits>(new string[] { "--stopbits", "-s" }, description: "stopBits of serial port", getDefaultValue: () => StopBits.One));
            command.Handler = CommandHandler.Create<InvocationContext, GlobalParams, SerialParams>(Run);
            return command;
        }

        private static int Run(InvocationContext context, GlobalParams globalParams, SerialParams @params)
        {
            Console.WriteLine(GetPortInfo(@params));
            var port = new SerialPort(@params.Name, @params.Baudrate, @params.Parity, @params.Databits, @params.Stopbits);
            port.Open();
            port.DataReceived += OnDataRecv;

            context.GetCancellationToken().Register(() => port.Close());

            while (!context.GetCancellationToken().IsCancellationRequested)
            {
                var data = Console.ReadLine();
                if (string.IsNullOrEmpty(data))
                {
                    continue;
                }
                var l = Encoding.UTF8.GetByteCount(data);
                var bytes = ArrayPool<byte>.Shared.Rent(l);
                Encoding.UTF8.GetBytes(data, 0, data.Length, bytes, 0);
                port.Write(bytes, 0, l);
                Interlocked.Add(ref totalSend, l);
                ArrayPool<byte>.Shared.Return(bytes);
                OutputSend(BitConverter.ToString(bytes, 0, l));
            }

            return 0;
        }

        private static void OnDataRecv(object sender, SerialDataReceivedEventArgs e)
        {
            var port = ((SerialPort)sender);
            var l = port.BytesToRead;
            var bytes = ArrayPool<byte>.Shared.Rent(l);
            port.Read(bytes, 0, l);
            Interlocked.Add(ref totalRecv, l);
            OutputRecv(BitConverter.ToString(bytes, 0, l));
        }

        private static string GetPortInfo(SerialParams @params)
        {
            return $"{"open".Pastel(Color.Gray)} {@params.Name.Pastel(Color.LightGreen)} {$"{@params.Baudrate},{GetParity(@params.Parity)},{@params.Databits},{GetStopbits(@params.Stopbits)}".Pastel(Color.Fuchsia)}";

            string GetParity(Parity p) => p switch
            {
                Parity.None => "N",
                Parity.Odd => "O",
                Parity.Even => "E",
                Parity.Mark => "M",
                Parity.Space => "S",
                _ => ""
            };

            string GetStopbits(StopBits s) => s switch
            {
                StopBits.One => "1",
                StopBits.Two => "2",
                StopBits.OnePointFive => "1.5",
                _ => ""
            };
        }

        private static void OutputRecv(string recv)
        {
            Console.WriteLine($"{"Total Recv:".Pastel(Color.Gray)}{totalRecv.ToString().Pastel(Color.Gray)}> {recv.Pastel(Color.LightBlue)}");
        }

        private static void OutputSend(string send)
        {
            Console.WriteLine($"{"Total Send:".Pastel(Color.Gray)}{totalSend.ToString().Pastel(Color.Gray)}> {send.Pastel(Color.LightGreen)}");
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
}