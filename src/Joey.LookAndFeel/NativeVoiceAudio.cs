using System.Buffers.Binary;

namespace Joey.LookAndFeel;

/// <summary>Bounded RIFF/WAVE metadata, independent of decoder or audio device.
/// Byte-rate duration is diagnostic, not the native x87 duration value.
/// No PCM samples are retained or exported.</summary>
public readonly record struct NativeWaveInfo(ushort Format, ushort Channels,
    uint SampleRate, uint ByteRate, ushort BlockAlign, ushort BitsPerSample, uint DataBytes)
{
    public double ByteRateDurationMilliseconds => DataBytes * 1000.0 / ByteRate;

    public static NativeWaveInfo Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8) ||
            !bytes.Slice(8,4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Expected RIFF/WAVE header");
        long end = 8L + BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (end < 12 || end > bytes.Length)
            throw new InvalidDataException("Truncated RIFF container");

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
        if(format==1 && (bits==0 || bits%8!=0 || channels*(long)bits/8!=align ||
            rate*(long)align!=byteRate || data%align!=0))
            throw new InvalidDataException("Inconsistent PCM WAVE format or data length");
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
