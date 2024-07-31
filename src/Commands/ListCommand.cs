using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Drawing;
using CoreLib.IO.Ports;
using Pastel;

namespace SerialportCli.Commands;

internal static class ListCommand
{
    private const int PAGE_SIZE = 8;

    public static Command Build()
    {
        var command = new Command("list", "list serial port.")
        {
            Handler = CommandHandler.Create<LogLevel, InvocationContext>(Run)
        };
        return command;
    }

    private static int Run(LogLevel logLevel, InvocationContext context)
    {
        var ports = SerialPortExtension.GetPortNames();
        var pages = GetPagesCount(ports.Length);

        var pagesPad = pages.ToString().Length + 1;
        if (ports.Any())
        {
            var namePad = ports.Max(i => i.Length) + 1;
            for (int i = 0; i < pages; i++)
            {
                Console.WriteLine("{0} {1}", i.ToString().PadLeft(pagesPad).Pastel(Color.Gray), string.Join(" ", ports.Skip(PAGE_SIZE * i).Take(PAGE_SIZE).Select(e => $"{e.PadRight(namePad).Pastel(Color.LightGreen)}")));
            }
        }
        Console.WriteLine($"{"Total:".Pastel(Color.Gray)} {ports.Length.ToString().Pastel(Color.DarkRed)}");

        return 0;
    }

    private static int GetPagesCount(int totalCount)
    {
        var total = Math.Max(totalCount, 0);
        return (0 == total % PAGE_SIZE) switch
        {
            true => total / PAGE_SIZE,
            false => total / PAGE_SIZE + 1
        };
    }
}
