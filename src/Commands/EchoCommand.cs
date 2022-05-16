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
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using System.Linq;

    internal static class EchoCommand
    {
        private static GlobalParams globalParams;
        private static SerialParams serialParams;
        private static EchoParams echoParams;
        private static long totalRecv;
        private static long totalSend;
        private static BlockingCollection<(byte[] d, int l)> recvQueue = new BlockingCollection<(byte[] d, int l)>();

        public static Command Build()
        {
            var command = new Command("echo", "connect to a serial port and echo receive.");
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
            command.AddOption(new Option<int?>(new string[] { "--init-bytes" }, description: "send bytes when open serial port"));
            command.Handler = CommandHandler.Create<InvocationContext, GlobalParams, SerialParams, EchoParams>(Run);
            return command;
        }

        private static async Task<int> Run(InvocationContext context, GlobalParams globalParams, SerialParams serialParams, EchoParams echoParams)
        {
            EchoCommand.globalParams = globalParams;
            EchoCommand.serialParams = serialParams;
            EchoCommand.echoParams = echoParams;
            Console.WriteLine(SerialPortUtils.GetPortInfo(serialParams));
            var port = SerialPortUtils.CreatePort(serialParams);
            port.Open();
            port.DataReceived += OnDataRecv;

            var processSendTask = Task.Run(() => ProcessSend(port, context.GetCancellationToken()));
            var outputTask = Task.Run(() => OutputLoop(context.GetCancellationToken()));

            var waiter = new TaskCompletionSource<int>();
            context.GetCancellationToken().Register(() =>
            {
                waiter.TrySetResult(0);
                port.Close();
            });

            if (echoParams.InitBytes.HasValue)
            {
                Console.WriteLine($"send init bytes {echoParams.InitBytes}");
                var bytes = ArrayPool<byte>.Shared.Rent(echoParams.InitBytes.Value);
                bytes.AsSpan().Fill(1);
                recvQueue.TryAdd((bytes, echoParams.InitBytes.Value));
            }

            await Task.WhenAll(waiter.Task, processSendTask, outputTask);
            OutPut();

            return 0;
        }

        private static void OnDataRecv(object sender, SerialDataReceivedEventArgs e)
        {
            var port = ((SerialPort)sender);
            var l = port.BytesToRead;
            var bytes = ArrayPool<byte>.Shared.Rent(l);
            port.Read(bytes, 0, l);
            Interlocked.Add(ref totalRecv, l);
            recvQueue.TryAdd((bytes, l));
        }

        private static void ProcessSend(SerialPort port, CancellationToken token)
        {
            try
            {
                foreach (var data in recvQueue.GetConsumingEnumerable(token))
                {
                    port.Write(data.d, 0, data.l);
                    ArrayPool<byte>.Shared.Return(data.d);
                    Interlocked.Add(ref totalSend, data.l);
                }
            }
            catch (System.OperationCanceledException) { }
        }

        private async static void OutputLoop(CancellationToken token)
        {
            try
            {
                Console.CursorVisible = false;
                while (!token.IsCancellationRequested)
                {
                    OutPut();
                    await Task.Delay(100);
                }
            }
            finally
            {
                Console.CursorVisible = true;
            }
        }

        private static void OutPut()
        {
            var output = $"{"Total:".Pastel(Color.Gray)}{totalRecv.ToString().Pastel(Color.DarkRed)} => {totalSend.ToString().Pastel(Color.DarkRed)}";
            if (EchoCommand.globalParams.NoAnsi)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(output);
            }
            else
            {
                Console.Write($"\u001b[100D{output}");
            }
        }

        internal class EchoParams
        {

            public int? InitBytes { get; set; }

        }

    }
}