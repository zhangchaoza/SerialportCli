using System.Drawing;
using System.IO.Ports;
using CoreLib.IO.Ports;
using CoreLib.Logging;
using Pastel;

namespace SerialportCli.Commands;

internal static class SerialPortUtils
{
    public static string GetPortInfo(SerialParams @params)
    {
        return $"{"open".Pastel(Color.Gray)} {@params.Port.Pastel(Color.LightGreen)} {$"{@params.BaudRate},{GetParity(@params.Parity)},{@params.DataBits},{GetStopbits(@params.StopBits)}".Pastel(Color.Fuchsia)}";

        string GetParity(Parity p) => p switch
        {
            Parity.None => "N",
            Parity.Odd => "O",
            Parity.Even => "E",
            Parity.Mark => "M",
            Parity.Space => "S",
            _ => ""
        };

        string GetStopbits(StopBits s) => s switch
        {
            StopBits.One => "1",
            StopBits.Two => "2",
            StopBits.OnePointFive => "1.5",
            _ => ""
        };
    }

    public static SerialPortWrapper CreatePort(SerialParams @params)
    {
        return new SerialPortWrapper(
            new SerialConnectInfo(@params.Port, @params.BaudRate, @params.Parity, @params.DataBits, @params.StopBits),
            readTimeout: 1000,
            writeTimeout: 1000,
            new EmptyLogger()
        );
    }
}