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
    using System.Runtime.InteropServices;
    using System.IO;
    using SerialportCli.Extensions;
    using SerialportCli.Natives;

    internal static class ReplCommand
    {

        private static long totalRecv;
        private static long totalSend;
        private static SerialParams serialParams;
        private static ReplParams replParams;

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
            command.AddOption(new Option<bool>(new string[] { "--string" }, description: "output string", getDefaultValue: () => false));
            command.Handler = CommandHandler.Create<InvocationContext, GlobalParams, SerialParams, ReplParams>(Run);
            return command;
        }

        private static int Run(InvocationContext context, GlobalParams globalParams, SerialParams serialParams, ReplParams replParams)
        {
            ReplCommand.serialParams = serialParams;
            ReplCommand.replParams = replParams;
            Console.WriteLine(GetPortInfo(serialParams));
            var port = new SerialPort(serialParams.Name, serialParams.Baudrate, serialParams.Parity, serialParams.Databits, serialParams.Stopbits)
            {
                // Handshake = Handshake.XOnXOff,
                // RtsEnable = true,
                // ReadTimeout = 250,
                // WriteTimeout = 250,
            };
            port.Open();
            port.DataReceived += OnDataRecv;

            context.GetCancellationToken().Register(() =>
            {
                port.Close();
                var handle = kernel32.GetStdHandle(kernel32.STD_INPUT_HANDLE);
                kernel32.CancelIoEx(handle, IntPtr.Zero);
            });

            while (!context.GetCancellationToken().IsCancellationRequested)
            {
                if (!ConsoleExtension.SafeReadline(out var line))
                {
                    break;
                }
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                // add newline
                line = line + "\r\n";

                var l = Encoding.UTF8.GetByteCount(line);
                var bytes = ArrayPool<byte>.Shared.Rent(l);
                Encoding.UTF8.GetBytes(line, 0, line.Length, bytes, 0);
                port.Write(bytes, 0, l);
                Interlocked.Add(ref totalSend, l);
                OutputSend(line, bytes, l);
                ArrayPool<byte>.Shared.Return(bytes);
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
            OutputRecv(bytes, l);
            ArrayPool<byte>.Shared.Return(bytes);
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

        private static void OutputRecv(byte[] bytes, int length)
        {
            if (replParams.String)
            {
                var s = System.Text.Encoding.UTF8.GetString(bytes, 0, length);
                Console.Write(s);
            }
            else
            {
                var recv = BitConverter.ToString(bytes, 0, length);
                Console.WriteLine($"{"Total Recv:".Pastel(Color.Gray)}{totalRecv.ToString().Pastel(Color.Gray)}> {recv.Pastel(Color.LightBlue)}");
            }
        }

        private static void OutputSend(string line, byte[] bytes, int length)
        {
            if (!replParams.String)
            {
                var send = BitConverter.ToString(bytes, 0, length);
                Console.WriteLine($"{"Total Send:".Pastel(Color.Gray)}{totalSend.ToString().Pastel(Color.Gray)}> {send.Pastel(Color.LightGreen)}");
            }
        }

        internal class ReplParams
        {

            public bool @String { get; set; }

        }

    }
}