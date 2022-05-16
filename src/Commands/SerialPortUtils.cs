using System.Drawing;
using System.IO.Ports;
using Pastel;

namespace SerialportCli;

internal static class SerialPortUtils
{
    public static string GetPortInfo(SerialParams @params)
    {
        return $"{"open".Pastel(Color.Gray)} {@params.Name.Pastel(Color.LightGreen)} {$"{@params.Baudrate},{GetParity(@params.Parity)},{@params.Databits},{GetStopbits(@params.Stopbits)}".Pastel(Color.Fuchsia)}";

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

    public static SerialPort CreatePort(SerialParams @params)
    {
        return new SerialPort(@params.Name, @params.Baudrate, @params.Parity, @params.Databits, @params.Stopbits)
        {
            // Handshake = Handshake.XOnXOff,
            // RtsEnable = true,
            // ReadTimeout = 250,
            // WriteTimeout = 250,
        };
    }

}

