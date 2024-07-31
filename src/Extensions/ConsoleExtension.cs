namespace SerialportCli.Extensions;

using System;

internal static class ConsoleExtension
{
    public static bool SafeReadLine(out string? line)
    {
        line = default;
        try
        {
            line = Console.ReadLine();
            return true;
        }
        // Handle the exception when the operation is canceled
        catch (InvalidOperationException)
        {
            // Console.WriteLine("Operation canceled");
            return false;
        }
        catch (OperationCanceledException)
        {
            // Console.WriteLine("Operation canceled");
            return false;
        }
    }
}