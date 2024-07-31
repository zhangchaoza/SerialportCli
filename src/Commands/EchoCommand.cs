using System.Collections.Concurrent;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Drawing;
using System.IO.Ports;
using CoreLib.IO.Buffers;
using CoreLib.IO.Ports;
using CoreLib.Sync;
using Pastel;

namespace SerialportCli.Commands;

internal static class EchoCommand
{
    private static GlobalParams? globalParams;
    private static SerialParams? serialParams;
    private static EchoParams? echoParams;
    private static long totalRecv;
    private static long totalSend;
    private static readonly BlockingCollection<Arc<IMemoryBuffer>> RecvQueue = new();

    public static Command Build()
    {
        var command = new Command("echo", "connect to a serial port and echo receive.");
        command.AddArgument(new Argument<string>("port", description: "port name of serial port"));
        command.AddOption(new Option<int>(["--baudrate", "-b"], description: "baudrate of serial port", getDefaultValue: () => SerialParams.DEFAULT_BAUDRATE));
        command.AddOption(new Option<Parity>(
            aliases: ["--parity", "-p"],
            description: "Parity of serial port.",
            parseArgument: r =>
            {
                if (r.Tokens.Any())
                {
                    return Enum.GetValues<Parity>().First(i => Enum.GetName(i)!.StartsWith(r.Tokens[0].Value, StringComparison.OrdinalIgnoreCase));
                }

                return SerialParams.DEFAULT_PARITY;
            },
            isDefault: true));
        command.AddOption(new Option<int>(["--databits", "-d"], description: "databits of serial port", getDefaultValue: () => SerialParams.DEFAULT_DATABITS));
        command.AddOption(new Option<StopBits>(["--stopbits", "-s"], description: "stopBits of serial port", getDefaultValue: () => SerialParams.DEFAULT_STOPBITS));
        command.AddOption(new Option<int?>(["--init-bytes"], description: "send bytes when open serial port"));
        command.Handler = CommandHandler.Create<InvocationContext, GlobalParams, SerialParams, EchoParams>(Run);
        return command;
    }

    private static async Task<int> Run(InvocationContext context, GlobalParams globalParams, SerialParams serialParams, EchoParams echoParams)
    {
        EchoCommand.globalParams = globalParams;
        EchoCommand.serialParams = serialParams;
        EchoCommand.echoParams = echoParams;
        Console.WriteLine(SerialPortUtils.GetPortInfo(EchoCommand.serialParams));
        var port = SerialPortUtils.CreatePort(EchoCommand.serialParams);
        port.Open();
        port.DataReceived += ProcessData;

        var processSendTask = Task.Run(async () => await ProcessSend(port, context.GetCancellationToken()));
        var outputTask = Task.Run(() => OutputLoop(context.GetCancellationToken()));

        var waiter = new TaskCompletionSource<int>();
        await using var reg = context.GetCancellationToken().Register(() =>
        {
            waiter.TrySetResult(0);
            port.Close();
        });

        if (EchoCommand.echoParams.InitBytes.HasValue)
        {
            using var arc = Arc<IMemoryBuffer>.CreateNew(MemoryBuffer.Create(EchoCommand.echoParams.InitBytes.Value));
            Random.Shared.NextBytes(arc.Value.Span);
            RecvQueue.TryAdd(arc.Clone());
        }

        await Task.WhenAll(waiter.Task, processSendTask, outputTask);
        OutPut();

        return 0;
    }

    private static Task ProcessData(AsyncSerialDataReceivedEventHandlerArgs data, CancellationToken cancellationToken)
    {
        var bufferArc = data.Buffer.Clone();
        Interlocked.Add(ref totalRecv, bufferArc.Value.Length);
        if (!RecvQueue.TryAdd(bufferArc))
        {
            bufferArc.Dispose();
        }
        return Task.CompletedTask;
    }

    private static async Task ProcessSend(SerialPortWrapper port, CancellationToken token)
    {
        try
        {
            foreach (var data in RecvQueue.GetConsumingEnumerable(token))
            {
                using var _ = data;
                await port.WriteAsync(data.Value.Memory, token);
                Interlocked.Add(ref totalSend, data.Value.Length);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async void OutputLoop(CancellationToken token)
    {
        try
        {
            try { Console.CursorVisible = false; } catch { }
            while (!token.IsCancellationRequested)
            {
                OutPut();
                await Task.Delay(100, token);
            }
        }
        finally
        {
            try { Console.CursorVisible = true; } catch { }
        }
    }

    private static void OutPut()
    {
        var output = $"{"Total:".Pastel(Color.Gray)}{totalRecv.ToString().Pastel(Color.DarkRed)} => {totalSend.ToString().Pastel(Color.DarkRed)}";
        if (globalParams!.NoAnsi)
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
