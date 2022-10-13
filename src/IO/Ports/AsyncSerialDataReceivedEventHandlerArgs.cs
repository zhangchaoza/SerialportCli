namespace SerialportCli.IO.Ports;
using AsyncEventHandlers;

public class AsyncSerialDataReceivedEventHandlerArgs : AsyncEventArgs
{

    public AsyncSerialDataReceivedEventHandlerArgs(MemoryBuffer buffer)
    {
        Buffer = buffer;
    }

    public MemoryBuffer Buffer { get; }
}