using System;
using System.Buffers;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using Pastel;
using SerialportCli.Report;

namespace SerialportCli;

public class TimerCommand
{
    private static Randomizer rand = new Randomizer();
    private static GlobalParams globalParams;
    private static SerialParams serialParams;
    private static FakeParams fakeParams;
    private static long totalRecv;
    private static long totalSend;

    public static Command Build()
    {
        var command = new Command("timer", "connect to a serial port and timer receive.");
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
        command.AddOption(new Option<int?>(new string[] { "--fake-length" }, description: "length of faked bytes.", getDefaultValue: () => 8));
        command.AddOption(new Option<uint>(new string[] { "--interval" }, description: "write data interval.", getDefaultValue: () => 1000));
        command.AddOption(new Option<long>(new string[] { "--timeout" }, description: "data fake timeout (ms).-1 means infinite.", getDefaultValue: () => 60000));
        command.AddOption(new Option<string>(new string[] { "--report-path" }, description: "path of report.(like file://sp_report.csv or file:///C:/sp_report.csv)", getDefaultValue: () => "file://sp_report.csv"));
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
        port.DataReceived += OnDataRecv;

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(fakeParams.Timeout));
        var token = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, context.GetCancellationToken()).Token;

        var processSendTask = Task.Run(async () => await ProcessSend(port, token));
        var outputTask = Task.Run(() => OutputLoop(token));

        var waiter = new TaskCompletionSource<int>();
        token.Register(() =>
        {
            waiter.TrySetResult(0);
            port.Close();
        });

        await Task.WhenAll(waiter.Task, processSendTask, outputTask);
        OutPut();
        Console.WriteLine();
        sp.Stop();

        // save report
        ReportUtils.SaveReport(fakeParams.ReportPath, serialParams.Name, totalRecv, totalSend, sp.ElapsedMilliseconds);

        return 0;
    }

    private static void OnDataRecv(object sender, SerialDataReceivedEventArgs e)
    {
        var port = ((SerialPort)sender);
        var l = port.BytesToRead;
        var bytes = ArrayPool<byte>.Shared.Rent(l);
        port.Read(bytes, 0, l);
        Interlocked.Add(ref totalRecv, l);
    }

    private static async Task ProcessSend(SerialPort port, CancellationToken token)
    {
        try
        {
            var _interval = TimeSpan.FromMilliseconds(fakeParams.Interval);
            while (!token.IsCancellationRequested)
            {
                var fakeBuffer = rand.Bytes(fakeParams.FakeLength);
                port.Write(fakeBuffer, 0, fakeBuffer.Length);
                Interlocked.Add(ref totalSend, fakeBuffer.Length);
                await Task.Delay(_interval, token);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    private async static void OutputLoop(CancellationToken token)
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
        if (TimerCommand.globalParams.NoAnsi)
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
        public uint Interval { get; set; }

        public long Timeout { get; set; }

        public string ReportPath { get; set; }

        public int FakeLength { get; set; }
    }
}