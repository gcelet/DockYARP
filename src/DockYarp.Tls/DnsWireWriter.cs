namespace DockYarp.Tls;

using System;
using System.IO;
using System.Text;

/// <summary>Writes DNS wire-format primitives (RFC 1035 §3/§4) to a growable buffer.</summary>
/// <remarks>Not a general-purpose DNS encoder — only what <see cref="DnsUpdateMessage"/> needs: 16/32-bit
/// big-endian integers, uncompressed domain names, and length-prefixed character-strings.</remarks>
internal sealed class DnsWireWriter
{
    private readonly MemoryStream buffer = new();

    /// <summary>Gets the number of bytes written so far.</summary>
    public int Length => (int)buffer.Length;

    /// <summary>Writes a big-endian 16-bit unsigned integer.</summary>
    public void WriteUInt16(ushort value)
    {
        buffer.WriteByte((byte)(value >> 8));
        buffer.WriteByte((byte)value);
    }

    /// <summary>Writes a big-endian 32-bit unsigned integer.</summary>
    public void WriteUInt32(uint value)
    {
        buffer.WriteByte((byte)(value >> 24));
        buffer.WriteByte((byte)(value >> 16));
        buffer.WriteByte((byte)(value >> 8));
        buffer.WriteByte((byte)value);
    }

    /// <summary>Writes a big-endian 48-bit unsigned integer (the TSIG "Time Signed" field).</summary>
    public void WriteUInt48(long value)
    {
        for (int shift = 40; shift >= 0; shift -= 8)
        {
            buffer.WriteByte((byte)(value >> shift));
        }
    }

    /// <summary>Writes raw bytes as-is.</summary>
    public void WriteBytes(ReadOnlySpan<byte> bytes) => buffer.Write(bytes);

    /// <summary>Writes an uncompressed domain name: each dot-separated label prefixed by its length,
    /// terminated by a zero-length label. A trailing dot on <paramref name="name"/> is tolerated.</summary>
    public void WriteName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string trimmed = name.TrimEnd('.');
        if (trimmed.Length > 0)
        {
            foreach (string label in trimmed.Split('.'))
            {
                byte[] labelBytes = Encoding.ASCII.GetBytes(label);
                if (labelBytes.Length is 0 or > 63)
                {
                    throw new ArgumentException($"DNS label '{label}' must be 1-63 bytes.", nameof(name));
                }

                buffer.WriteByte((byte)labelBytes.Length);
                buffer.Write(labelBytes);
            }
        }

        buffer.WriteByte(0); // Root label terminator.
    }

    /// <summary>Writes a single DNS character-string: a one-byte length prefix followed by up to 255 bytes.</summary>
    public void WriteCharacterString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > 255)
        {
            throw new ArgumentException("A DNS character-string cannot exceed 255 bytes.", nameof(text));
        }

        buffer.WriteByte((byte)bytes.Length);
        buffer.Write(bytes);
    }

    /// <summary>Returns the bytes written so far.</summary>
    public byte[] ToArray() => buffer.ToArray();
}
