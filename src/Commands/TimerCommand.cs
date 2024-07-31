using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using CoreLib.IO.Buffers;
using CoreLib.IO.Ports;
using Pastel;
using SerialportCli.Report;

namespace SerialportCli.Commands;

public class TimerCommand
{
    private static GlobalParams? globalParams;
    private static SerialParams? serialParams;
    private static FakeParams? fakeParams;
    private static long totalRecv;
    private static long totalSend;

    public static Command Build()
    {
        var command = new Command("timer", "connect to a serial port and timer receive.");
        command.AddArgument(new Argument<string>("port", description: "port name of serial port"));
        command.AddOption(new Option<int>(["--baudrate", "-b"], description: "baudrate of serial port", getDefaultValue: () => SerialParams.DEFAULT_BAUDRATE));
        command.AddOption(new Option<Parity>(
            ["--parity", "-p"],
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
        command.AddOption(new Option<int?>(["--fake-length"], description: "length of faked bytes.", getDefaultValue: () => FakeParams.DEFAULT_FAKE_LENGTH));
        command.AddOption(new Option<uint>(["--interval"], description: "write data interval.", getDefaultValue: () => FakeParams.DEFAULT_INTERVAL));
        command.AddOption(new Option<long>(["--timeout"], description: "data fake timeout (ms).-1 means infinite.", getDefaultValue: () => FakeParams.DEFAULT_TIMEOUT));
        command.AddOption(new Option<long>(["--output-timeout"], description: "output timeout (ms) after send task has stopped.-1 means infinite.", getDefaultValue: () => FakeParams.DEFAULT_OUTPUT_TIMEOUT));
        command.AddOption(new Option<string>(["--report-path"], description: "path of report.(like file://sp_report.csv or file:///C:/sp_report.csv)", getDefaultValue: () => FakeParams.DEFAULT_REPORT_PATH));
        command.Handler = CommandHandler.Create<InvocationContext, GlobalParams, SerialParams, TimerCommand, FakeParams>(Run);
        return command;
    }

    private static async Task<int> Run(InvocationContext context, GlobalParams globalParams, SerialParams serialParams, TimerCommand timerParams, FakeParams fakeParams)
    {
        TimerCommand.globalParams = globalParams;
        TimerCommand.serialParams = serialParams;
        TimerCommand.fakeParams = fakeParams;
        Console.WriteLine(SerialPortUtils.GetPortInfo(TimerCommand.serialParams));
        var port = SerialPortUtils.CreatePort(TimerCommand.serialParams);
        var sp = new Stopwatch();
        sp.Start();
        port.Open();
        port.DataReceived += ProcessData;

        using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(TimerCommand.fakeParams.Timeout));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsTimeout.Token, context.GetCancellationToken());
        using var ctsOut = new CancellationTokenSource();

        var processSendTask = Task.Run(async () => await ProcessSend(port, cts.Token), cts.Token);
        var outputTask = Task.Run(() => OutputLoop(ctsOut.Token), ctsOut.Token);

        {
            var waiter = new TaskCompletionSource<int>();
            await using var reg = cts.Token.Register(() => waiter.TrySetResult(0));
            await Task.WhenAll(waiter.Task, processSendTask);
        }

        Console.WriteLine();
        if (TimerCommand.fakeParams.OutputTimeout >= 0)
        {
            Console.WriteLine("Send task had stopped, wait Output task finished.");
            var waiter = new TaskCompletionSource();
            await using var reg = ctsOut.Token.Register(() => waiter.TrySetResult());
            ctsOut.CancelAfter(TimeSpan.FromMilliseconds(TimerCommand.fakeParams.OutputTimeout));
            await waiter.Task;
        }
        else
        {
            Console.WriteLine("Send task had stopped, press Enter to stop Output task if you want");
            Console.ReadLine();
        }
        port.Close();
        await ctsOut.CancelAsync();
        await outputTask;

        OutPut();
        Console.WriteLine();
        sp.Stop();

        // save report
        ReportUtils.SaveReport(TimerCommand.fakeParams.ReportPath, TimerCommand.serialParams.Port, totalRecv, totalSend, sp.ElapsedMilliseconds);

        return 0;
    }

    private static Task ProcessData(AsyncSerialDataReceivedEventHandlerArgs data, CancellationToken cancellationToken)
    {
        Interlocked.Add(ref totalRecv, data.Buffer.Value.Length);
        return Task.CompletedTask;
    }

    private static async Task ProcessSend(SerialPortWrapper port, CancellationToken token)
    {
        try
        {
            var _interval = TimeSpan.FromMilliseconds(fakeParams!.Interval);
            while (!token.IsCancellationRequested)
            {
                using IMemoryBuffer buffer = MemoryBuffer.Create(fakeParams.FakeLength);
                Random.Shared.NextBytes(buffer.Span);
                await port.WriteAsync(buffer.Memory, token);
                Interlocked.Add(ref totalSend, buffer.Length);
                await Task.Delay(_interval, token);
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
        var output = $"{"Total:".Pastel(Color.Gray)}RX: {totalRecv.ToString().Pastel(Color.DarkRed)} ,TX: {totalSend.ToString().Pastel(Color.DarkRed)}";
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

    internal class FakeParams
    {
        public const uint DEFAULT_INTERVAL = 1000;
        public const long DEFAULT_TIMEOUT = 60000;
        public const long DEFAULT_OUTPUT_TIMEOUT = 6000;
        public const string DEFAULT_REPORT_PATH = "file://sp_report.csv";
        public const int DEFAULT_FAKE_LENGTH = 8;

        public uint Interval { get; set; } = DEFAULT_INTERVAL;

        public long Timeout { get; set; } = DEFAULT_TIMEOUT;

        public long OutputTimeout { get; set; } = DEFAULT_OUTPUT_TIMEOUT;

        public string ReportPath { get; set; } = DEFAULT_REPORT_PATH;

        public int FakeLength { get; set; } = DEFAULT_FAKE_LENGTH;
    }
}