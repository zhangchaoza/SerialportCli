namespace SerialportCli;

using CoreLib.IO.Ports;
using Pastel;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Drawing;
using System.Linq;

internal static class ListCommand
{
    private const int PAGE_SIZE = 8;

    public static Command Build()
    {
        var command = new Command("list", "list serial port.");
        command.Handler = CommandHandler.Create<LogLevel, InvocationContext>(Run);
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
                Console.WriteLine("{0} {1}", i.ToString().PadLeft(pagesPad).Pastel(Color.Gray), string.Join(" ", ports.Skip(PAGE_SIZE * i).Take(PAGE_SIZE).Select(i => $"{i.PadRight(namePad).Pastel(Color.LightGreen)}")));
            }
        }
        Console.WriteLine($"{"Total:".Pastel(Color.Gray)} {ports.Length.ToString().Pastel(Color.DarkRed)}");

        return 0;
    }

    private static int GetPagesCount(int totalCount)
    {
        var total = Math.Max(totalCount, 0);
        return (0 == (total % PAGE_SIZE)) switch
        {
            true => (int)(total / PAGE_SIZE),
            false => (int)(total / PAGE_SIZE + 1)
        };
    }
}
