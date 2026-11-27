using System.Buffers.Binary;

namespace Joey.LookAndFeel;

/// <summary>Bounded RIFF/WAVE metadata, independent of decoder or audio device.
/// Byte-rate duration is diagnostic, not the native x87 duration value.
/// No PCM samples are retained or exported.</summary>
public readonly record struct NativeWaveInfo(ushort Format, ushort Channels,
    uint SampleRate, uint ByteRate, ushort BlockAlign, ushort BitsPerSample, uint DataBytes)
{
    public double ByteRateDurationMilliseconds => DataBytes * 1000.0 / ByteRate;

    public static NativeWaveInfo Read(ReadOnlySpan<byte> bytes) =>
        ReadCore(bytes, voiceArchive: false, out _);

    /// <summary>Reads declared metadata. When canonicalEnd is set, use
    /// PrepareVoiceArchiveEntry to produce a standalone RIFF.</summary>
    public static NativeWaveInfo ReadVoiceArchiveEntry(ReadOnlySpan<byte> bytes, out int? canonicalEnd) =>
        ReadCore(bytes, voiceArchive: true, out canonicalEnd);

    /// <summary>Produces a bounded decoder input without modifying the archive bytes.</summary>
    public static byte[] PrepareVoiceArchiveEntry(byte[] bytes, out NativeWaveInfo info)
    {
        info = ReadVoiceArchiveEntry(bytes, out int? canonicalEnd);
        if (canonicalEnd is not int end) return bytes;

        var canonical = bytes[..end];
        BinaryPrimitives.WriteUInt32LittleEndian(canonical.AsSpan(4), (uint)(end - 8));
        BinaryPrimitives.WriteUInt32LittleEndian(canonical.AsSpan(42), (uint)(end - 46));
        return canonical;
    }

    private static NativeWaveInfo ReadCore(ReadOnlySpan<byte> bytes, bool voiceArchive, out int? canonicalEnd)
    {
        canonicalEnd = null;
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8) ||
            !bytes.Slice(8,4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Expected RIFF/WAVE header");
        long end = 8L + BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (end < 12 || end > bytes.Length)
            throw new InvalidDataException("Truncated RIFF container");

        // The Voice.dat PCM variant has fmt size 18, no extension data,
        // and precisely 4 bytes inside/40 outside RIFF after its data
        // chunk. Native duration reader 005C0F80 stops at the data size.
        bool archivePcm = voiceArchive && end >= 50 && bytes.Length - end == 40 &&
            bytes.Slice(12, 4).SequenceEqual("fmt "u8) &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]) == 18 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[20..]) == 1 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[22..]) is 1 or 2 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[34..]) == 16 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[36..]) == 0 &&
            bytes.Slice(38, 4).SequenceEqual("data"u8) &&
            46L + BinaryPrimitives.ReadUInt32LittleEndian(bytes[42..]) == end - 4;
        if (archivePcm)
        {
            end -= 4;
        }

        ushort format=0,channels=0,align=0,bits=0;
        uint rate=0,byteRate=0,data=0;
        bool hasFormat=false,hasData=false;
        long pos=12;
        while (pos<end)
        {
            if(end-pos<8)throw new InvalidDataException("Truncated RIFF chunk header");
            var header=bytes.Slice((int)pos,8);
            uint size=BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            long next=pos+8L+size+(size&1u);
            if(next>end)throw new InvalidDataException("Truncated RIFF chunk or padding");
            var body=bytes.Slice((int)(pos+8),(int)size);
            if(header[..4].SequenceEqual("fmt "u8))
            {
                if(hasFormat||size<16)throw new InvalidDataException("Invalid or duplicate WAVE format");
                format=BinaryPrimitives.ReadUInt16LittleEndian(body);
                channels=BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                rate=BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
                byteRate=BinaryPrimitives.ReadUInt32LittleEndian(body[8..]);
                align=BinaryPrimitives.ReadUInt16LittleEndian(body[12..]);
                bits=BinaryPrimitives.ReadUInt16LittleEndian(body[14..]);
                hasFormat=true;
            }
            else if(header[..4].SequenceEqual("data"u8))
            {
                if(hasData)throw new InvalidDataException("Duplicate WAVE data chunk");
                data=size;
                hasData=true;
            }
            pos=next;
        }
        if(!hasFormat||!hasData||format==0||channels==0||rate==0||byteRate==0||align==0)
            throw new InvalidDataException("Incomplete or invalid WAVE metadata");
        bool partialStereoFrame = archivePcm && channels == 2 && align == 4 &&
            data > align && data % align == 2;
        if(format==1 && (bits==0 || bits%8!=0 || channels*(long)bits/8!=align ||
            rate*(long)align!=byteRate || (data%align!=0 && !partialStereoFrame)))
            throw new InvalidDataException("Inconsistent PCM WAVE format or data length");
        // The local decoder accepts whole frames. Keep the declared size in
        // metadata and omit the final unpaired sample from its input.
        if (archivePcm) canonicalEnd = (int)end - (partialStereoFrame ? 2 : 0);
        return new(format,channels,rate,byteRate,align,bits,data);
    }
}

public enum NativeAudioBank { Internal, Stream }
public readonly record struct NativeAudioRequest(NativeAudioBank Bank,int Index,int TransitionParameter);

/// <summary>Raw dispatch/volume arithmetic; not a mapping from DAT file order.</summary>
public static class NativeAudioRouting
{
    public static NativeAudioRequest? Resolve(bool enabled,uint request,
        int internalCount,int streamCount,int transition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(internalCount);
        ArgumentOutOfRangeException.ThrowIfNegative(streamCount);
        if(!enabled)return null;
        uint family=request&0xf0000000u;
        if(family==0x10000000u)
        {
            int i=(int)(request&0xfff);
            return i<internalCount?new(NativeAudioBank.Internal,i,0):null;
        }
        if(family==0x20000000u)
        {
            int i=(int)(request&0x0fffffffu);
            return i<streamCount?new(NativeAudioBank.Stream,i,Math.Max(5,transition)):null;
        }
        return null;
    }

    // 005C0F40: integer 0..127 -> DirectSound hundredths of a dB.
    // This is not raylib's linear gain. Invalid input returns 0 natively.
    public static int DirectSoundVolume(int level) =>
        level is >=0 and <128 ? level*10000/127-10000 : 0;

    // 005C1360/005C13F0: preserve the intermediate integer truncation.
    public static int StreamVolumeLevel(int descriptor,int master,int secondary)
    {
        if(descriptor is <0 or >127 || master is <0 or >127 || secondary is <0 or >127)
            throw new ArgumentOutOfRangeException(nameof(descriptor),"Expected 0..127 levels");
        return (descriptor*master/127)*secondary/127;
    }
}
