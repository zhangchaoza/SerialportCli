//namespace SerialportCli.Utils;

//using System;
//using System.Buffers;
//using System.Linq;

//public static class ByteArrayUtil
//{
//    public static string ToHexString(this byte[]? bytes, int index = 0, int? length = null)
//    {
//        if (null == bytes || !bytes.Any())
//        {
//            return string.Empty;
//        }

//        ReadOnlySpan<byte> span;
//        span = new ReadOnlySpan<byte>(bytes).Slice(index, length ?? (bytes.Length - index));
//        return ToHexString(span);
//    }

//    public static string ToHexString(this IMemoryOwner<byte> memory, int? length = null) => ToHexString(memory.Memory.Slice(0, length ?? memory.Memory.Length));

//    public static string ToHexString(this Memory<byte> memory) => ToHexString(memory.Span);

//    public static string ToHexString(this ReadOnlySpan<byte> span)
//    {
//        var sb = new System.Text.StringBuilder(span.Length * 3);

//        var blockSize = 16;
//        for (var i = 0; i < Math.Ceiling(span.Length / (double)blockSize); i++)
//        {
//            sb.Append(i.ToString("D3"));
//            sb.Append(' ');
//            var block = span.Slice(i * blockSize, Math.Min(blockSize, span.Length - i * blockSize));
//            foreach (var b in block)
//            {
//                sb.Append(b.ToString("X2"));
//                sb.Append(' ');
//            }
//            for (int m = 0; m < blockSize - block.Length; m++)
//            {
//                sb.Append("  ");
//                sb.Append(' ');
//            }
//            foreach (var b in block)
//            {
//                sb.Append(WashingChar((char)b));
//            }
//            sb.AppendLine();
//        }
//        return sb.ToString().TrimEnd(' ', '\r', '\n');
//    }

//    public static string ToSimpleHexString(this byte[]? bytes, int index = 0, int? length = null)
//    {
//        if (null == bytes || !bytes.Any())
//        {
//            return "[]";
//        }

//        ReadOnlySpan<byte> span;
//        span = new ReadOnlySpan<byte>(bytes).Slice(index, length ?? (bytes.Length - index));
//        return ToSimpleHexString(span);
//    }

//    public static string ToSimpleHexString(this Memory<byte> memory) => ToSimpleHexString(memory.Span);

//    public static string ToSimpleHexString(this ReadOnlySpan<byte> span)
//    {
//        var sb = new System.Text.StringBuilder(span.Length * 3 + 2);
//        sb.Append('[');
//        for (int i = 0; i < span.Length; i++)
//        {
//            sb.Append(span[i].ToString("X2"));
//            if (i < span.Length - 1)
//            {
//                sb.Append(' ');
//            }
//        }
//        sb.Append(']');
//        return sb.ToString();
//    }

//    public static string ToSimpleHexString(this ReadOnlySequence<byte> sequence)
//    {
//        var sb = new System.Text.StringBuilder((int)(sequence.Length * 3 + 2));
//        sb.Append('[');

//        foreach (var sm in sequence)
//        {
//            for (int i = 0; i < sm.Span.Length; i++)
//            {
//                sb.Append(sm.Span[i].ToString("X2"));
//                if (i < sm.Span.Length - 1)
//                {
//                    sb.Append(' ');
//                }
//            }
//        }
//        sb.Append(']');
//        return sb.ToString();
//    }

//    private static char WashingChar(char c)
//    {
//        if ((int)c <= 126 && (int)c >= 32)
//        {
//            return c;
//        }
//        else
//        {
//            return '_';
//            // return $@"\u{(int)c:x4}";
//        }
//    }
//}