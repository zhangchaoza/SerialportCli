using CoreLib.IO;
using CoreLib.IO.Ports;
using Pastel;
using SerialportCli.Report;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;

namespace SerialportCli;

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
        command.AddOption(new Option<int>(new string[] { "--baudrate", "-b" }, description: "baudrate of serial port", getDefaultValue: () => SerialParams.DEFAULT_BAUDRATE));
        command.AddOption(new Option<Parity>(
            new string[] { "--parity", "-p" },
            description: "Parity of serial port.",
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
        command.AddOption(new Option<int?>(new string[] { "--fake-length" }, description: "length of faked bytes.", getDefaultValue: () => FakeParams.DEFAULT_FAKE_LENGTH));
        command.AddOption(new Option<uint>(new string[] { "--interval" }, description: "write data interval.", getDefaultValue: () => FakeParams.DEFAULT_INTERVAL));
        command.AddOption(new Option<long>(new string[] { "--timeout" }, description: "data fake timeout (ms).-1 means infinite.", getDefaultValue: () => FakeParams.DEFAULT_TIMEOUT));
        command.AddOption(new Option<string>(new string[] { "--report-path" }, description: "path of report.(like file://sp_report.csv or file:///C:/sp_report.csv)", getDefaultValue: () => FakeParams.DEFAULT_REPORT_PATH));
        command.Handler = CommandHandler.Create<InvocationContext, GlobalParams, SerialParams, TimerCommand, FakeParams>(Run);
        return command;
    }

    private static async Task<int> Run(InvocationContext context, GlobalParams globalParams, SerialParams serialParams, TimerCommand timerParams, FakeParams fakeParams)
    {
        TimerCommand.globalParams = globalParams;
        TimerCommand.serialParams = serialParams;
        TimerCommand.fakeParams = fakeParams;
        Console.WriteLine(SerialPortUtils.GetPortInfo(serialParams));
        var port = SerialPortUtils.CreatePort(serialParams);
        var sp = new Stopwatch();
        sp.Start();
        port.Open();
        port.DataReceived += ProcessData;

        using var ctsTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(fakeParams.Timeout));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsTimeout.Token, context.GetCancellationToken());

        var processSendTask = Task.Run(async () => await ProcessSend(port, cts.Token));
        //var processSendTask = Task.Delay(-1);
        var outputTask = Task.Run(() => OutputLoop(cts.Token));
        //var outputTask = Task.Delay(-1);

        var waiter = new TaskCompletionSource<int>();
        using var reg = cts.Token.Register(() =>
        {
            waiter.TrySetResult(0);
            port.Close();
        });

        await Task.WhenAll(waiter.Task, processSendTask, outputTask);
        OutPut();
        Console.WriteLine();
        sp.Stop();

        // save report
        ReportUtils.SaveReport(fakeParams.ReportPath, serialParams.Port, totalRecv, totalSend, sp.ElapsedMilliseconds);

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
                using var buffer = MemoryBuffer.Create(fakeParams.FakeLength);
                Random.Shared.NextBytes(buffer.Span);
                await port.WriteAsync(buffer.Memory);
                Interlocked.Add(ref totalSend, buffer.Length);
                await Task.Delay(_interval, token);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    private static async void OutputLoop(CancellationToken token)
    {
        try
        {
            try { Console.CursorVisible = false; } catch { }
            while (!token.IsCancellationRequested)
            {
                OutPut();
                await Task.Delay(100);
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
        if (TimerCommand.globalParams!.NoAnsi)
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
        public const string DEFAULT_REPORT_PATH = "file://sp_report.csv";
        public const int DEFAULT_FAKE_LENGTH = 8;

        public uint Interval { get; set; } = DEFAULT_INTERVAL;

        public long Timeout { get; set; } = DEFAULT_TIMEOUT;

        public string ReportPath { get; set; } = DEFAULT_REPORT_PATH;

        public int FakeLength { get; set; } = DEFAULT_FAKE_LENGTH;
    }
}