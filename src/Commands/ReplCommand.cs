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
    using System.CommandLine.NamingConventionBinder;
    using SerialportCli.IO.Ports;
    using SerialportCli.IO;
    using SerialportCli.Utils;

    internal static class ReplCommand
    {

        private static long totalRecv;
        private static long totalSend;
        private static SerialParams? serialParams;
        private static ReplParams? replParams;

        public static Command Build()
        {
            var command = new Command("repl", "read and write data in repl mode.");
            command.AddArgument(new Argument<string>("port", description: "port name of serial port"));
            command.AddOption(new Option<int>(new string[] { "--baudrate", "-b" }, description: "baudrate of serial port", getDefaultValue: () => SerialParams.DEFAULT_BAUDRATE));
            command.AddOption(new Option<Parity>(new string[] { "--parity", "-p" }, description: "Parity of serial port.",
                parseArgument: r =>
                {
                    if (r.Tokens.Any())
                    {
                        return Enum.GetValues<Parity>().First(i => Enum.GetName<Parity>(i)!.StartsWith(r.Tokens[0].Value, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        return SerialParams.DEFAULT_PARITY;
                    }
                },
                isDefault: true));
            command.AddOption(new Option<int>(new string[] { "--databits", "-d" }, description: "databits of serial port", getDefaultValue: () => SerialParams.DEFAULT_DATABITS));
            command.AddOption(new Option<StopBits>(new string[] { "--stopbits", "-s" }, description: "stopBits of serial port", getDefaultValue: () => SerialParams.DEFAULT_STOPBITS));
            command.AddOption(new Option<bool>(new string[] { "--string" }, description: "output string", getDefaultValue: () => false));
            command.Handler = CommandHandler.Create<InvocationContext, GlobalParams, SerialParams, ReplParams>(Run);
            return command;
        }

        private static async Task<int> Run(InvocationContext context, GlobalParams globalParams, SerialParams serialParams, ReplParams replParams)
        {
            ReplCommand.serialParams = serialParams;
            ReplCommand.replParams = replParams;
            Console.WriteLine(SerialPortUtils.GetPortInfo(serialParams));
            var port = SerialPortUtils.CreatePort(serialParams);
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
                if (!ConsoleExtension.SafeReadLine(out var line))
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
                using var buffer = MemoryBuffer.Create(l);
                Encoding.UTF8.GetBytes(line, buffer.Span);
                await port.WriteAsync(buffer.Memory);
                Interlocked.Add(ref totalSend, buffer.Length);
                OutputSend(buffer);
            }

            return 0;
        }

        private static Task OnDataRecv(object? sender, AsyncSerialDataReceivedEventHandlerArgs e)
        {
            Interlocked.Add(ref totalRecv, e.Buffer.Length);
            OutputRecv(e.Buffer);
            e.Buffer.Dispose();
            return Task.CompletedTask;
        }

        private static void OutputRecv(MemoryBuffer buffer)
        {
            if (replParams!.String)
            {
                var s = System.Text.Encoding.UTF8.GetString(buffer.Span);
                Console.Write(s);
            }
            else
            {
                var recv = buffer.Memory.ToSimpleHexString();
                Console.WriteLine($"{"Total Recv:".Pastel(Color.Gray)}{totalRecv.ToString().Pastel(Color.Gray)}> {recv.Pastel(Color.LightBlue)}");
            }
        }

        private static void OutputSend(MemoryBuffer buffer)
        {
            if (!replParams!.String)
            {
                var send = buffer.Memory.ToSimpleHexString();
                Console.WriteLine($"{"Total Send:".Pastel(Color.Gray)}{totalSend.ToString().Pastel(Color.Gray)}> {send.Pastel(Color.LightGreen)}");
            }
        }

        internal class ReplParams
        {

            public bool @String { get; set; }

        }

    }
}