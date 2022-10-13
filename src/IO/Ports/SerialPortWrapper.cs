namespace SerialportCli.IO.Ports;

using System;
using System.IO.Ports;
using System.Threading.Channels;
using AsyncEventHandlers;

public class SerialPortWrapper : IDisposable
{
    private SerialPort port;
    private Channel<int> recvEventChannel = Channel.CreateUnbounded<int>();
    private CancellationTokenSource cts = new CancellationTokenSource();
    private readonly AsyncEventHandler<AsyncSerialDataReceivedEventHandlerArgs> dataReceived = new AsyncEventHandler<AsyncSerialDataReceivedEventHandlerArgs>();

    public SerialPortWrapper(SerialConnectInfo connectInfo, int readTimeout, int writeTimeout)
    {
        port = new SerialPort(connectInfo.Port, connectInfo.BaudRate, connectInfo.Parity, connectInfo.DataBits, connectInfo.StopBits);
        port.ReadTimeout = readTimeout;
        port.WriteTimeout = writeTimeout;
        // port.Handshake = Handshake.XOnXOff;
        // port.RtsEnable = true;
        port.DataReceived += OnDataRecv;
    }

    public event AsyncEvent<AsyncSerialDataReceivedEventHandlerArgs> DataReceived
    {
        add { dataReceived.Register(value); }
        remove { dataReceived.Unregister(value); }
    }

    public void Open()
    {
        if (cts.IsCancellationRequested)// re-open
        {
            cts = new CancellationTokenSource();
            recvEventChannel = Channel.CreateUnbounded<int>();
        }

        // event loop
        _ = Task.Run(async () =>
        {
            await foreach (var s in recvEventChannel.Reader.ReadAllAsync(cts.Token))
            {
                try
                {
                    if (port.BytesToRead > 0)
                    {
                        var buffer = MemoryBuffer.Create(port.BytesToRead);
                        await ReadAsync(buffer.Memory, cts.Token);
                        await dataReceived.InvokeAsync(this, new AsyncSerialDataReceivedEventHandlerArgs(buffer));
                    }
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (System.Exception)
                {
                    throw;
                }
            }
        });
        port.Open();
    }

    public void Close()
    {
        cts.Cancel();
        port.Close();
    }

    public async Task<int> ReadAsync(Memory<byte> memory, CancellationToken token = default(CancellationToken))
    {
        CheckAvailable();

        if (memory.Length == 0)
        {
            throw new ArgumentException("buffer length can not be 0.");
        }

        using var readCts = new CancellationTokenSource(port.ReadTimeout);// 每次读取的超时时间
        var _token = CancellationTokenSource.CreateLinkedTokenSource(readCts.Token, token).Token;
        using var _tr = _token.Register(() =>
        {
            if (port.IsOpen)
            {
                port.DiscardInBuffer();// 必要,可以触发ReadAsync cancel
            }
        });

        try
        {
            return await port.BaseStream.ReadAsync(memory, _token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (readCts.IsCancellationRequested)
        {
            throw new TimeoutException("The asynchronous read operation timed out.");
        }
        catch (IOException) when (readCts.IsCancellationRequested && !token.IsCancellationRequested)
        {
            throw new TimeoutException("The asynchronous read operation timed out.");
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task WriteAsync(Memory<byte> memory, CancellationToken token = default(CancellationToken))
    {
        CheckAvailable();

        using var writeCts = new CancellationTokenSource(port.WriteTimeout);// 每次写入的超时时间
        var _token = CancellationTokenSource.CreateLinkedTokenSource(writeCts.Token, token).Token;
        using var _tr = _token.Register(() =>
        {
            port.DiscardOutBuffer();// 必要,可以触发WriteAsync cancel
        });

        try
        {
            await port.BaseStream.WriteAsync(memory, token);
            await port.BaseStream.FlushAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (writeCts.IsCancellationRequested)
        {
            throw new TimeoutException("The asynchronous write operation timed out.");
        }
        catch (IOException) when (writeCts.IsCancellationRequested && !token.IsCancellationRequested)
        {
            throw new TimeoutException("The asynchronous write operation timed out.");
        }
        catch (Exception)
        {
            throw;
        }
    }

    public void Dispose()
    {
        port.Dispose();
    }

    private void CheckAvailable()
    {
        if (!port.IsOpen)
        {
            throw new InvalidOperationException("port must open.");
        }
    }

    private int GetReadIntervalTimeout(int baudrate)
    {
        if (baudrate > 20000)
        {
            return 100;
        }
        else if (baudrate <= 2400)
        {
            return 400;
        }
        else
        {
            return Convert.ToInt32(Math.Ceiling(400 - (baudrate * 0.01223)));
        }
    }

    private void OnDataRecv(object sender, SerialDataReceivedEventArgs e)
    {
        if (dataReceived.Callbacks.Count > 0)
        {
            recvEventChannel.Writer.TryWrite(0);
        }
    }

}
