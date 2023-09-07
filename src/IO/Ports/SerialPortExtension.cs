namespace SerialportCli.IO.Ports;

using System.IO.Ports;
using SerialportCli.Utils;

public static class SerialPortExtension
{
    public static string[] GetPortNames()
    {
        return SerialPort.GetPortNames().OrderBy(i => i, new NumericStringComparer()).ToArray();
    }
}
