using System.Buffers.Binary;
using System.Text.Json;

namespace ClipRange.Host;

// Chrome frames each message as a 4-byte native-endian length followed by UTF-8 JSON.
sealed class NativePort(Stream input, Stream output)
{
    readonly Lock writeLock = new();

    public Request? Read()
    {
        Span<byte> header = stackalloc byte[4];
        if (input.ReadAtLeast(header, 4, throwOnEndOfStream: false) < 4)
            return null;

        var body = new byte[BinaryPrimitives.ReadInt32LittleEndian(header)];
        input.ReadExactly(body);
        return JsonSerializer.Deserialize(body, Json.Default.Request);
    }

    public void Write(Reply reply)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(reply, Json.Default.Reply);
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);

        lock (writeLock)
        {
            output.Write(header);
            output.Write(body);
            output.Flush();
        }
    }
}
