using System.Buffers.Binary;
using Joey.LookAndFeel;

internal static class NativeVoiceAudioChecks
{
    // A synthetic mono PCM WAV with an odd-size JUNK chunk and pad.
    private static byte[] Fixture()
    {
        byte[] bytes=new byte[58];
        "RIFF"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),50);
        "WAVE"u8.CopyTo(bytes.AsSpan(8));
        "JUNK"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16),1);
        bytes[20]=42;
        "fmt "u8.CopyTo(bytes.AsSpan(22));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(26),16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30),1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32),1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(34),8000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(38),16000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42),2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44),16);
        "data"u8.CopyTo(bytes.AsSpan(46));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(50),4);
        return bytes;
    }

    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        byte[] b=Fixture();
        var info=NativeWaveInfo.Read(b);
        check(info==new NativeWaveInfo(1,1,8000,16000,2,16,4)&&
            info.ByteRateDurationMilliseconds==0.25,
            "V18: bounded RIFF parsing and odd chunk padding");
        reject(()=>NativeWaveInfo.Read(b[..^1]),"V18: truncated RIFF");
        byte[] invalid=(byte[])b.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(invalid.AsSpan(16),uint.MaxValue);
        reject(()=>NativeWaveInfo.Read(invalid),"V18: reject oversized chunk");
        byte[] badRate=(byte[])b.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(badRate.AsSpan(38),0);
        reject(()=>NativeWaveInfo.Read(badRate),"V18: reject zero byte rate");
        byte[] badAlign=(byte[])b.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(badAlign.AsSpan(42),1);
        reject(()=>NativeWaveInfo.Read(badAlign),"V18: reject inconsistent PCM alignment");
        byte[] noData=(byte[])b.Clone();
        "JUNK"u8.CopyTo(noData.AsSpan(46));
        reject(()=>NativeWaveInfo.Read(noData),"V18: reject missing data chunk");
        byte[] voice = new byte[94];
        b.AsSpan(0, 12).CopyTo(voice);
        b.AsSpan(22, 24).CopyTo(voice.AsSpan(12));
        b.AsSpan(46, 12).CopyTo(voice.AsSpan(38));
        BinaryPrimitives.WriteUInt32LittleEndian(voice.AsSpan(4), 46);
        BinaryPrimitives.WriteUInt32LittleEndian(voice.AsSpan(16), 18);
        voice.AsSpan(50).Fill(42);
        check(NativeWaveInfo.ReadVoiceArchiveEntry(voice, out int? canonicalEnd) == info &&
            canonicalEnd == 50, "V18: Voice.dat PCM metadata ends at the data chunk");
        check(NativeWaveInfo.ReadVoiceArchiveEntry(b, out canonicalEnd) == info &&
            canonicalEnd is null, "V18: valid RIFF entries require no adaptation");
        reject(() => NativeWaveInfo.Read(voice), "V18: generic RIFF rejects the Voice.dat suffix");
        reject(() => NativeWaveInfo.ReadVoiceArchiveEntry(voice[..^1], out _),
            "V18: Voice.dat must have the observed suffix size");
        byte[] inconsistentVoice = (byte[])voice.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(inconsistentVoice.AsSpan(42), 5);
        reject(() => NativeWaveInfo.ReadVoiceArchiveEntry(inconsistentVoice, out _),
            "V18: Voice.dat data size must reach the observed boundary");
        inconsistentVoice = (byte[])voice.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(inconsistentVoice.AsSpan(32), 1);
        reject(() => NativeWaveInfo.ReadVoiceArchiveEntry(inconsistentVoice, out _),
            "V18: Voice.dat adaptation preserves PCM validation");
        check(NativeAudioRouting.Resolve(true,0x10001045,70,20,99)==
            new NativeAudioRequest(NativeAudioBank.Internal,69,0),
            "V18: Internal uses low twelve bits, ignores stream transition");
        check(NativeAudioRouting.Resolve(true,0x2000000b,70,20,0)==
            new NativeAudioRequest(NativeAudioBank.Stream,11,5)&&
            NativeAudioRouting.Resolve(true,0x2000000b,70,20,9)==
            new NativeAudioRequest(NativeAudioBank.Stream,11,9),
            "V18: Stream uses low 28 bits and minimum transition");
        check(NativeAudioRouting.Resolve(true,0x2000100b,70,20,5) is null&&
            NativeAudioRouting.Resolve(true,0x10000046,70,20,5) is null&&
            NativeAudioRouting.Resolve(false,0x10000001,70,20,5) is null&&
            NativeAudioRouting.Resolve(true,0x30000001,70,20,5) is null,
            "V18: reject absent descriptor, disabled audio and unknown family");
        check(NativeAudioRouting.DirectSoundVolume(0)==-10000&&
            NativeAudioRouting.DirectSoundVolume(127)==0&&
            NativeAudioRouting.DirectSoundVolume(64)==-4961&&
            NativeAudioRouting.DirectSoundVolume(-1)==0,
            "V18: native DirectSound volume integer conversion");
        check(NativeAudioRouting.StreamVolumeLevel(127,63,64)==31&&
            NativeAudioRouting.StreamVolumeLevel(64,63,127)==31&&
            NativeAudioRouting.StreamVolumeLevel(127,127,127)==127,
            "V18: Stream volume preserves intermediate truncation");
    }
}
