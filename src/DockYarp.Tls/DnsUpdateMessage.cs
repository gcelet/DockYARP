namespace DockYarp.Tls;

using System;

/// <summary>Builds a signed RFC 2136 DNS UPDATE message adding or deleting a single TXT record.</summary>
/// <remarks>
/// Hand-rolled rather than via a third-party library: the only maintained NuGet candidate
/// (<c>ARSoft.Tools.Net</c>) targets net6.0 and depends on <c>BouncyCastle.Cryptography</c>, which conflicts
/// with <c>Portable.BouncyCastle</c> (already pinned for CRL parsing). A DNS UPDATE message (RFC 2136 §2,
/// built on the RFC 1035 message format) and a TSIG record (RFC 8945 §4) are both small, well-specified
/// binary structures — this needs only BCL primitives.
/// </remarks>
internal static class DnsUpdateMessage
{
    private const ushort TypeSoa = 6;
    private const ushort TypeTxt = 16;
    private const ushort TypeTsig = 250;
    private const ushort ClassIn = 1;
    private const ushort ClassNone = 254;
    private const ushort ClassAny = 255;
    private const ushort TsigFudgeSeconds = 300;

    /// <summary>Builds a signed UPDATE message that adds a TXT record.</summary>
    /// <param name="zone">The zone apex the update applies to (e.g. <c>example.com.</c>).</param>
    /// <param name="name">The record name (e.g. <c>_acme-challenge.example.com.</c>).</param>
    /// <param name="value">The TXT record's text value.</param>
    /// <param name="ttl">The TTL to set on the added record.</param>
    /// <param name="key">The TSIG key signing this message.</param>
    /// <returns>The complete, signed message bytes.</returns>
    public static byte[] BuildAddTxt(string zone, string name, string value, TimeSpan ttl, TsigKey key)
    {
        DnsWireWriter writer = new();
        ushort id = WriteHeaderAndZone(writer, zone, prCount: 0, upCount: 1);

        writer.WriteName(name);
        writer.WriteUInt16(TypeTxt);
        writer.WriteUInt16(ClassIn);
        writer.WriteUInt32((uint)ttl.TotalSeconds);
        byte[] rdata = BuildTxtRdata(value);
        writer.WriteUInt16((ushort)rdata.Length);
        writer.WriteBytes(rdata);

        return Sign(writer, id, key);
    }

    /// <summary>Builds a signed UPDATE message that deletes a previously added TXT record.</summary>
    /// <param name="zone">The zone apex the update applies to (e.g. <c>example.com.</c>).</param>
    /// <param name="name">The record name (e.g. <c>_acme-challenge.example.com.</c>).</param>
    /// <param name="value">The exact TXT record's text value to delete (must match what was added).</param>
    /// <param name="key">The TSIG key signing this message.</param>
    /// <returns>The complete, signed message bytes.</returns>
    public static byte[] BuildDeleteTxt(string zone, string name, string value, TsigKey key)
    {
        DnsWireWriter writer = new();
        ushort id = WriteHeaderAndZone(writer, zone, prCount: 0, upCount: 1);

        // RFC 2136 §2.5.4 "Delete An RR From An RRset": CLASS=NONE, TTL=0, RDATA=the exact RR being removed.
        writer.WriteName(name);
        writer.WriteUInt16(TypeTxt);
        writer.WriteUInt16(ClassNone);
        writer.WriteUInt32(0);
        byte[] rdata = BuildTxtRdata(value);
        writer.WriteUInt16((ushort)rdata.Length);
        writer.WriteBytes(rdata);

        return Sign(writer, id, key);
    }

    private static byte[] BuildTxtRdata(string value)
    {
        DnsWireWriter rdataWriter = new();
        rdataWriter.WriteCharacterString(value);
        return rdataWriter.ToArray();
    }

    /// <summary>Writes the header and single-entry zone section, returning the generated query ID.</summary>
    private static ushort WriteHeaderAndZone(DnsWireWriter writer, string zone, ushort prCount, ushort upCount)
    {
        ushort id = (ushort)Random.Shared.Next(ushort.MinValue, ushort.MaxValue + 1);
        writer.WriteUInt16(id);
        writer.WriteBytes([0x28, 0x00]); // QR=0, Opcode=UPDATE(5); all other flags/RCODE=0.
        writer.WriteUInt16(1); // ZOCOUNT
        writer.WriteUInt16(prCount); // PRCOUNT
        writer.WriteUInt16(upCount); // UPCOUNT
        writer.WriteUInt16(0); // ADCOUNT (patched implicitly: TSIG is the only additional record, added by Sign).

        writer.WriteName(zone);
        writer.WriteUInt16(TypeSoa); // ZTYPE
        writer.WriteUInt16(ClassIn); // ZCLASS
        return id;
    }

    /// <summary>Appends a TSIG record (RFC 8945 §4) to the message and patches ADCOUNT to include it.</summary>
    private static byte[] Sign(DnsWireWriter writer, ushort id, TsigKey key)
    {
        byte[] unsigned = writer.ToArray();
        long timeSigned = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string algorithmWireName = TsigAlgorithms.ToWireName(key.Algorithm);
        string keyName = key.Name.ToLowerInvariant();

        DnsWireWriter macInput = new();
        macInput.WriteBytes(unsigned);
        macInput.WriteName(keyName);
        macInput.WriteUInt16(ClassAny);
        macInput.WriteUInt32(0); // TTL
        macInput.WriteName(algorithmWireName);
        macInput.WriteUInt48(timeSigned);
        macInput.WriteUInt16(TsigFudgeSeconds);
        macInput.WriteUInt16(0); // Error
        macInput.WriteUInt16(0); // Other Len (no Other Data)

        byte[] mac = key.ComputeMac(macInput.ToArray());

        DnsWireWriter tsigRr = new();
        tsigRr.WriteName(keyName);
        tsigRr.WriteUInt16(TypeTsig);
        tsigRr.WriteUInt16(ClassAny);
        tsigRr.WriteUInt32(0); // TTL

        DnsWireWriter tsigRdata = new();
        tsigRdata.WriteName(algorithmWireName);
        tsigRdata.WriteUInt48(timeSigned);
        tsigRdata.WriteUInt16(TsigFudgeSeconds);
        tsigRdata.WriteUInt16((ushort)mac.Length);
        tsigRdata.WriteBytes(mac);
        tsigRdata.WriteUInt16(id); // Original ID
        tsigRdata.WriteUInt16(0); // Error
        tsigRdata.WriteUInt16(0); // Other Len
        byte[] rdataBytes = tsigRdata.ToArray();
        tsigRr.WriteUInt16((ushort)rdataBytes.Length);
        tsigRr.WriteBytes(rdataBytes);

        byte[] signed = new byte[unsigned.Length + tsigRr.Length];
        unsigned.CopyTo(signed, 0);
        tsigRr.ToArray().CopyTo(signed, unsigned.Length);

        // Patch ADCOUNT (header bytes 10-11) from 0 to 1 — the TSIG record is the sole additional record.
        signed[10] = 0x00;
        signed[11] = 0x01;
        return signed;
    }
}
