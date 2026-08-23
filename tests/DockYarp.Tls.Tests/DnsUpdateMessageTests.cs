namespace DockYarp.Tls.Tests;

using System;
using System.Security.Cryptography;
using System.Text;

using AwesomeAssertions;

using DockYarp.Tls;

/// <summary>Structural tests for the hand-rolled RFC 2136 UPDATE + RFC 8945 TSIG message builder — no
/// external library or live server involved. A minimal sequential reader (mirroring
/// <see cref="DnsWireWriter"/>'s uncompressed-name/no-jump format) parses the produced bytes back apart so
/// each field can be asserted directly against the RFC's own layout.</summary>
public sealed class DnsUpdateMessageTests
{
    private static readonly TsigKey Key = TsigKey.Parse("Acme-Key.", Convert.ToBase64String("s3cr3t-key-material"u8), "hmac-sha256");

    [Test]
    public void BuildAddTxt_HeaderAndZoneSectionAreCorrect()
    {
        byte[] message = DnsUpdateMessage.BuildAddTxt("example.com.", "_acme-challenge.example.com.", "abc123", TimeSpan.FromSeconds(60), Key);
        Reader reader = new(message);

        ushort id = reader.ReadUInt16();
        byte flagsHigh = reader.ReadByte();
        byte flagsLow = reader.ReadByte();
        (flagsHigh >> 3 & 0b1111).Should().Be(5, "Opcode must be UPDATE (5)");
        (flagsHigh >> 7 & 1).Should().Be(0, "QR must be 0 for a request");
        flagsLow.Should().Be(0);

        reader.ReadUInt16().Should().Be(1, "ZOCOUNT");
        reader.ReadUInt16().Should().Be(0, "PRCOUNT");
        reader.ReadUInt16().Should().Be(1, "UPCOUNT");
        reader.ReadUInt16().Should().Be(1, "ADCOUNT (the TSIG record)");

        reader.ReadName().Should().Be("example.com");
        reader.ReadUInt16().Should().Be(6, "ZTYPE must be SOA (6)");
        reader.ReadUInt16().Should().Be(1, "ZCLASS must be IN (1)");

        id.Should().NotBe(0, "a query ID of exactly 0 is astronomically unlikely and would suggest Random.Shared wasn't actually called");
    }

    [Test]
    public void BuildAddTxt_UpdateSectionAddsTxtRecordWithGivenTtlAndClassIn()
    {
        byte[] message = DnsUpdateMessage.BuildAddTxt("example.com.", "_acme-challenge.example.com.", "abc123", TimeSpan.FromSeconds(60), Key);
        Reader reader = SkipHeaderAndZone(message);

        reader.ReadName().Should().Be("_acme-challenge.example.com");
        reader.ReadUInt16().Should().Be(16, "TYPE must be TXT (16)");
        reader.ReadUInt16().Should().Be(1, "CLASS must be IN (1) for an add");
        reader.ReadUInt32().Should().Be(60, "TTL must match the requested TimeSpan");
        ushort rdLength = reader.ReadUInt16();
        byte[] rdata = reader.ReadBytes(rdLength);
        rdata[0].Should().Be((byte)"abc123".Length, "the RDATA is a single character-string");
        Encoding.UTF8.GetString(rdata, 1, rdata.Length - 1).Should().Be("abc123");
    }

    [Test]
    public void BuildDeleteTxt_UpdateSectionUsesClassNoneAndZeroTtl()
    {
        byte[] message = DnsUpdateMessage.BuildDeleteTxt("example.com.", "_acme-challenge.example.com.", "abc123", Key);
        Reader reader = SkipHeaderAndZone(message);

        reader.ReadName().Should().Be("_acme-challenge.example.com");
        reader.ReadUInt16().Should().Be(16, "TYPE must still be TXT (16)");
        reader.ReadUInt16().Should().Be(254, "CLASS must be NONE (254) — RFC 2136 §2.5.4 delete-one-RR form");
        reader.ReadUInt32().Should().Be(0, "TTL must be 0 for a delete");
    }

    [Test]
    public void BuildAddTxt_TsigRecordCarriesAValidSelfConsistentMac()
    {
        byte[] message = DnsUpdateMessage.BuildAddTxt("example.com.", "_acme-challenge.example.com.", "abc123", TimeSpan.FromSeconds(60), Key);

        (byte[] unsigned, int unsignedLength) = ExtractUnsignedPrefix(message);
        Reader tsigReader = new(message, unsignedLength);
        string tsigName = tsigReader.ReadName();
        tsigName.Should().Be("acme-key", "the TSIG owner name is the key name, canonicalized to lowercase");
        tsigReader.ReadUInt16().Should().Be(250, "TYPE must be TSIG (250)");
        tsigReader.ReadUInt16().Should().Be(255, "CLASS must be ANY (255)");
        tsigReader.ReadUInt32().Should().Be(0, "TTL must be 0");
        tsigReader.ReadUInt16(); // RDLENGTH, not needed further.

        string algorithmName = tsigReader.ReadName();
        algorithmName.Should().Be("hmac-sha256");
        long timeSigned = tsigReader.ReadUInt48();
        ushort fudge = tsigReader.ReadUInt16();
        fudge.Should().Be(300);
        ushort macSize = tsigReader.ReadUInt16();
        byte[] mac = tsigReader.ReadBytes(macSize);
        mac.Length.Should().Be(32, "HMAC-SHA256 produces a 32-byte MAC");
        ushort originalId = tsigReader.ReadUInt16();
        tsigReader.ReadUInt16().Should().Be(0, "Error must be 0 for a request");
        tsigReader.ReadUInt16().Should().Be(0, "Other Len must be 0");

        originalId.Should().Be(message[0] == 0 && message[1] == 0 ? originalId : (ushort)((message[0] << 8) | message[1]),
            "Original ID in the TSIG RDATA must match the header's query ID");

        // Recompute the MAC independently over the same canonical bytes RFC 8945 §4.3.1 defines, and confirm
        // it matches what the builder embedded — proves internal self-consistency of the signing logic.
        DnsWireWriter macInput = new();
        macInput.WriteBytes(unsigned);
        macInput.WriteName("acme-key");
        macInput.WriteUInt16(255);
        macInput.WriteUInt32(0);
        macInput.WriteName("hmac-sha256.");
        macInput.WriteUInt48(timeSigned);
        macInput.WriteUInt16(300);
        macInput.WriteUInt16(0);
        macInput.WriteUInt16(0);
        byte[] expectedMac = HMACSHA256.HashData("s3cr3t-key-material"u8, macInput.ToArray());
        mac.Should().BeEquivalentTo(expectedMac);
    }

    /// <summary>Returns the message bytes preceding the TSIG record, with ADCOUNT (header bytes 10-11)
    /// un-patched back to 0 — reconstructing exactly what <see cref="DnsUpdateMessage"/> fed into the MAC,
    /// since the final message has ADCOUNT already patched to include the TSIG record itself.</summary>
    private static (byte[] Unsigned, int Length) ExtractUnsignedPrefix(byte[] message)
    {
        Reader reader = SkipHeaderAndZone(message);
        reader.ReadName(); // Update RR name.
        reader.SkipBytes(2 + 2 + 4); // TYPE, CLASS, TTL.
        ushort updateRdLength = reader.ReadUInt16();
        reader.SkipBytes(updateRdLength);
        int length = reader.Position;

        byte[] unsigned = message[..length];
        unsigned[10] = 0x00;
        unsigned[11] = 0x00;
        return (unsigned, length);
    }

    private static Reader SkipHeaderAndZone(byte[] message)
    {
        Reader reader = new(message);
        reader.SkipBytes(12); // Header.
        reader.ReadName(); // ZNAME.
        reader.SkipBytes(4); // ZTYPE + ZCLASS.
        return reader;
    }

    /// <summary>A minimal sequential reader for the deterministic, uncompressed wire format
    /// <see cref="DnsWireWriter"/> produces — test-only, not a general DNS parser.</summary>
    private sealed class Reader(byte[] buffer, int position = 0)
    {
        public int Position { get; private set; } = position;

        public byte ReadByte() => buffer[Position++];

        public ushort ReadUInt16()
        {
            ushort value = (ushort)((buffer[Position] << 8) | buffer[Position + 1]);
            Position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            uint value = (uint)((buffer[Position] << 24) | (buffer[Position + 1] << 16) | (buffer[Position + 2] << 8) | buffer[Position + 3]);
            Position += 4;
            return value;
        }

        public long ReadUInt48()
        {
            long value = 0;
            for (int i = 0; i < 6; i++)
            {
                value = (value << 8) | buffer[Position + i];
            }

            Position += 6;
            return value;
        }

        public byte[] ReadBytes(int count)
        {
            byte[] result = buffer[Position..(Position + count)];
            Position += count;
            return result;
        }

        public void SkipBytes(int count) => Position += count;

        public string ReadName()
        {
            System.Collections.Generic.List<string> labels = [];
            while (true)
            {
                byte length = ReadByte();
                if (length == 0)
                {
                    break;
                }

                labels.Add(Encoding.ASCII.GetString(ReadBytes(length)));
            }

            return string.Join('.', labels);
        }
    }
}
