using SerialportCli.Commands;

namespace SerialportCli;

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Linq;

internal class Program
{
    private static int Main(string[] args)
    {
        Console.InputEncoding = new System.Text.UTF8Encoding();

        if (!args.Any())
        {
            args = ["-h"];
        }

        var rootCommand = new RootCommand("Serialport Cli tool")
        {
            ListCommand.Build(),
            EchoCommand.Build(),
            ReplCommand.Build(),
            TimerCommand.Build()
        };

        rootCommand.AddGlobalOption(new Option<LogLevel>(
            aliases: ["--Verbose", "-v"],
            description: "Set the verbosity level.",
            parseArgument: r =>
            {
                if (r.Tokens.Any())
                {
                    return Enum.GetValues<LogLevel>().First(i => Enum.GetName(i)!.StartsWith(r.Tokens[0].Value, StringComparison.OrdinalIgnoreCase));
                }

                return LogLevel.Normal;
            },
            isDefault: true));

        // rootCommand.AddGlobalOption(new Option<bool>("--no-colors", description: "disable colorizer output.", getDefaultValue: () => false));
        var noAnsiOption = new Option<bool>("--no-ansi", description: "disable ansi output.", getDefaultValue: () => false);
        rootCommand.AddGlobalOption(noAnsiOption);
        var pauseOption = new Option<bool>("--pause", description: "pause when finish execute command.", getDefaultValue: () => false);
        rootCommand.AddGlobalOption(new Option<bool>("--pause", description: "pause when finish execute command.", getDefaultValue: () => false));

        var builder = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .Build();

        ProcessBeforeInvoke(builder.Parse(args), noAnsiOption);

        var exitCode = builder.InvokeAsync(args).Result;
        Console.WriteLine();

        var pause = builder.Parse(args).GetValueForOption(pauseOption);
        if (pause)
        {
            Console.WriteLine($"Execute finished with exit code {exitCode},press any keys to exit.");
            Console.ReadLine();
        }

        return exitCode;
    }

    private static void ProcessBeforeInvoke(ParseResult parseResult, Option<bool> option)
    {
        #region colorizer

        var noAnsi = parseResult.GetValueForOption(option);
        if (noAnsi)
        {
            Pastel.ConsoleExtensions.Disable();
        }

        #endregion colorizer
    }
}