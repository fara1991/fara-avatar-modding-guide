using System.Buffers.Binary;
using FaraAvatarModdingGuide.Data;
using Xunit;

namespace FaraAvatarModdingGuide.Content.Tests;

/// <summary>PNG の寸法の読み取り（ContentService.ReadPngSize）。1 回の Read で全部を返さないストリームでも読めること。</summary>
public class PngSizeTests
{
    private static byte[] PngHeader(int width, int height)
    {
        var b = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0);
        "IHDR"u8.CopyTo(b.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(20), height);
        return b;
    }

    /// <summary>1 回の Read で 1 バイトだけ返すストリーム（ネットワークや圧縮のストリームに近い）。</summary>
    private sealed class TrickleStream(byte[] data) : Stream
    {
        private int position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (position >= data.Length || buffer.IsEmpty) return 0;
            buffer[0] = data[position++];
            return 1;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void Reads_the_size_even_when_each_read_returns_one_byte()
    {
        var size = ContentService.ReadPngSize(new TrickleStream(PngHeader(640, 480)));
        Assert.Equal((640, 480), size);
    }

    [Fact]
    public void A_stream_shorter_than_the_header_gives_no_size()
    {
        Assert.Null(ContentService.ReadPngSize(new MemoryStream(PngHeader(10, 10)[..20])));
    }

    [Fact]
    public void A_non_png_header_gives_no_size()
    {
        var bytes = PngHeader(10, 10);
        bytes[1] = 0;
        Assert.Null(ContentService.ReadPngSize(new MemoryStream(bytes)));
    }
}
