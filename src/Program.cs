namespace SerialportCli
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Parsing;
    using System.Linq;

    class Program
    {
        static int Main(string[] args)
        {
            if (!args.Any())
            {
                args = new string[] { "-h" };
            }
            var rootCommand = new RootCommand("Serialport Cli tool");

            var builder = new CommandLineBuilder(rootCommand)
                .AddCommand(ListCommand.Build())
                .AddCommand(EchoCommand.Build())
                .AddGlobalOption(new Option<LogLevel>(
                    new string[] { "--Verbose", "-v" },
                    description: "Set the verbosity level.",
                    parseArgument: r =>
                    {
                        if (r.Tokens.Any())
                        {
                            return Enum.GetValues<LogLevel>().First(i => Enum.GetName<LogLevel>(i).StartsWith(r.Tokens[0].Value, StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            return LogLevel.Normal;
                        }
                    },
                    isDefault: true))
                .AddOption(new Option<bool>("--no-colors", description: "disable colorizer output.", getDefaultValue: () => false))
                .AddGlobalOption(new Option<bool>("--pause", description: "pause when finish execute command.", getDefaultValue: () => false))
                .ParseResponseFileAs(responseFileHandling: ResponseFileHandling.ParseArgsAsSpaceSeparated)
                .UseDefaults()
                .Build();

            var exitCode = builder.InvokeAsync(args).Result;
            Console.WriteLine();

            var pause = builder.Parse(args).ValueForOption<bool>("--pause");
            if (pause)
            {
                Console.WriteLine($"Execute finished with exitcode {exitCode},press any keys to exit.");
                Console.ReadLine();
            }

            return exitCode;
        }
    }
}

