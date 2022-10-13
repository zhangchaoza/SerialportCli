namespace SerialportCli.IO;

using System;
using System.Buffers;

public record MemoryBuffer(IMemoryOwner<byte> Buffers, int Length) : IDisposable
{
    public Memory<byte> Memory => Buffers.Memory.Slice(0, Length);

    public Span<byte> Span => Memory.Span;

    public static MemoryBuffer Create(int length) => new MemoryBuffer(MemoryPool<byte>.Shared.Rent(length), length);

    public void Dispose()
    {
        Buffers.Dispose();
    }
}
