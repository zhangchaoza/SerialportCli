using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using CoreLib.IO.Buffers;
using CoreLib.IO.Ports;
using CoreLib.Sync;
using CoreLib.Utils;
using Pastel;
using SerialportCli.Extensions;
using SerialportCli.Natives;

namespace SerialportCli.Commands;

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
        command.AddOption(new Option<bool>(["--string"], description: "output string", getDefaultValue: () => false));
        command.Handler = CommandHandler.Create<InvocationContext, GlobalParams, SerialParams, ReplParams>(Run);
        return command;
    }

    private static async Task<int> Run(InvocationContext context, GlobalParams globalParams, SerialParams serialParams, ReplParams replParams)
    {
        ReplCommand.serialParams = serialParams;
        ReplCommand.replParams = replParams;
        Console.WriteLine(SerialPortUtils.GetPortInfo(ReplCommand.serialParams));
        var port = SerialPortUtils.CreatePort(ReplCommand.serialParams);
        port.Open();
        port.DataReceived += ProcessData;

        await using var reg = context.GetCancellationToken().Register(() =>
        {
            port.Close();
            var handle = Kernel32.GetStdHandle(Kernel32.STD_INPUT_HANDLE);
            Kernel32.CancelIoEx(handle, IntPtr.Zero);
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
            line += "\r\n";

            var l = Encoding.UTF8.GetByteCount(line);
            using IMemoryBuffer buffer = MemoryBuffer.Create(l);
            Encoding.UTF8.GetBytes(line, buffer.Span);
            await port.WriteAsync(buffer.Memory);
            Interlocked.Add(ref totalSend, buffer.Length);
            OutputSend(buffer);
        }

        return 0;
    }

    private static Task ProcessData(AsyncSerialDataReceivedEventHandlerArgs data, CancellationToken cancellationToken)
    {
        using var bufferArc = data.Buffer.Clone();
        Interlocked.Add(ref totalRecv, bufferArc.Value.Length);
        OutputRecv(bufferArc);
        return Task.CompletedTask;
    }

    private static void OutputRecv(Arc<IMemoryBuffer> buffer)
    {
        if (replParams!.String)
        {
            var s = Encoding.UTF8.GetString(buffer.Value.Span);
            Console.Write(s);
        }
        else
        {
            var recv = buffer.Value.Memory.ToSimpleHexString();
            Console.WriteLine($"{"Total Recv:".Pastel(Color.Gray)}{totalRecv.ToString().Pastel(Color.Gray)}> {recv.Pastel(Color.LightBlue)}");
        }
    }

    private static void OutputSend(IMemoryBuffer buffer)
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
