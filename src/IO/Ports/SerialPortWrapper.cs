namespace SerialportCli.IO.Ports;

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.IO.Ports;
using AsyncEventHandlers;

public delegate Task<ReadOnlySequence<byte>> ProcessReceivedHandler(ReadOnlySequence<byte> buffer, CancellationToken token = default);

public class SerialPortWrapper : IDisposable
{

    private SerialPort port;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private CancellationTokenSource ctsRead = new CancellationTokenSource();
    private Pipe readerPipe = new Pipe();
    private ProcessReceivedHandler? processReceivedHandler;
    // private readonly AsyncEventHandler<AsyncSerialDataReceivedEventHandlerArgs> dataReceived = new AsyncEventHandler<AsyncSerialDataReceivedEventHandlerArgs>();

    public SerialPortWrapper(SerialConnectInfo connectInfo, int readTimeout, int writeTimeout)
    {
        port = new SerialPort(connectInfo.Port, connectInfo.BaudRate, connectInfo.Parity, connectInfo.DataBits, connectInfo.StopBits);
        port.ReadTimeout = readTimeout;
        port.WriteTimeout = writeTimeout;
        // port.Handshake = Handshake.XOnXOff;
        // port.RtsEnable = true;
    }

    // public event AsyncEvent<AsyncSerialDataReceivedEventHandlerArgs> DataReceived
    // {
    //     add { dataReceived.Register(value); }
    //     remove { dataReceived.Unregister(value); }
    // }

    public ProcessReceivedHandler? ProcessReceivedHandler
    {
        get => processReceivedHandler;
        set
        {
            if (value is null)
            {
                ctsRead.Cancel();
            }
            else
            {
                ctsRead = new CancellationTokenSource();
                _ = Task.Run(ReadLoop);
            }
            processReceivedHandler = value;
        }
    }

    public void Open()
    {
        if (port.IsOpen)
        {
            throw new Exception("Port already opened.");
        }

        if (cts.IsCancellationRequested)// re-open
        {
            cts = new CancellationTokenSource();
        }
        port.Open();

        // recv pipe loop
        _ = Task.Run(RecvLoop);

        if (processReceivedHandler is not null && !ctsRead.IsCancellationRequested)
        {
            ProcessReceivedHandler = processReceivedHandler;
        }
    }

    public void Close()
    {
        cts.Cancel();
        port.Close();
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> memory, CancellationToken token = default(CancellationToken))
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

    public Task<int> ReadAsync(Memory<byte> memory, CancellationToken token = default(CancellationToken))
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        port.Dispose();
    }

    private async Task<int> ReadCoreAsync(Memory<byte> memory, CancellationToken token = default(CancellationToken))
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

    private async Task RecvLoop()
    {
        var _writer = readerPipe.Writer;
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var buffer = _writer.GetMemory(256);
                var l = await ReadCoreAsync(buffer, cts.Token);
                _writer.Advance(l);

                var result = await _writer.FlushAsync();
                if (result.IsCompleted)
                {
                    break;
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
        await _writer.CompleteAsync();
    }

    private async Task ReadLoop()
    {
        var _reader = readerPipe.Reader;
        var _cts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ctsRead.Token);
        while (!_cts.IsCancellationRequested)
        {
            var result = await _reader.ReadAsync(_cts.Token);
            var buffer = result.Buffer;
            var processBuffer = await (ProcessReceivedHandler?.Invoke(buffer, _cts.Token) ?? Task.FromResult(buffer.Slice(buffer.End)));
            _reader.AdvanceTo(processBuffer.Start, processBuffer.End);
            if (result.IsCompleted)
            {
                break;
            }
        }
        await _reader.CompleteAsync();
    }

}
