using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;

/// <summary>Opt-in audio lab. No duel state, window, native ID guesses or
/// rules scheduler. Audio remains inside the user's original archive.</summary>
internal static class VoiceAudioReview
{
    private const string InternalPrefix=@"joey\english\internal\";
    private sealed record Entry(string Name, uint Size, NativeWaveInfo? Info,
        double? EstimatedMilliseconds, string? Error);

    internal static int Run(string? path,bool inspect,string? review,bool mute)
    {
        try
        {
            if(inspect==(review is not null))
                throw new InvalidDataException("Choose --inspect-voice-dat OR --review-voice=<filename.wav>.");
            if(string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("Specify --voice-dat=/path/to/Voice.dat.");
            using var archive=new Archive(path);
            if(inspect)return Inspect(archive);
            if(mute)throw new InvalidDataException("--review-voice is incompatible with --mute-joey.");
            string filename=review!;
            if(filename.Length==0||filename.Contains('/')||filename.Contains('\\')||
                filename.Contains(':')||!filename.EndsWith(".wav",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Use an exact Internal WAV filename (e.g. joey_01.wav).");
            string name=InternalPrefix+filename;
            if(!archive.Entries.Any(e=>e.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Missing original voice resource: "+name);
            byte[] bytes=archive.Read(name);
            var info=NativeWaveInfo.ReadVoiceArchiveEntry(bytes, out int? canonicalEnd);
            Console.WriteLine($"[voice review] {name}: format={info.Format} rate={info.SampleRate} channels={info.Channels} bytes={info.DataBytes} estimated_ms={info.ByteRateDurationMilliseconds:F1}");
            if(canonicalEnd is int length)
            {
                Array.Resize(ref bytes, length);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)(length - 8));
            }
            return Play(bytes);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or
            UnauthorizedAccessException or KeyNotFoundException or OverflowException or
            DllNotFoundException or EntryPointNotFoundException)
        {
            Console.Error.WriteLine("[voice] "+ex.Message);
            return 1;
        }
    }

    private static int Inspect(Archive archive)
    {
        List<Entry> entries=[];
        foreach(var e in archive.Entries.Where(e=>e.Name.EndsWith(".wav",StringComparison.OrdinalIgnoreCase))
            .OrderBy(e=>e.Name,StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var info=NativeWaveInfo.ReadVoiceArchiveEntry(archive.Read(e.Name), out _);
                entries.Add(new(e.Name,e.Size,info,info.ByteRateDurationMilliseconds,null));
            }
            catch(Exception ex) when(ex is IOException or InvalidDataException or OverflowException)
            {
                entries.Add(new(e.Name,e.Size,null,null,ex.Message));
            }
        }
        if(entries.Count==0)throw new InvalidDataException("No WAV entries in supplied archive.");
        string output=Path.Combine("scratch","visual-review","voice-metadata.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output,JsonSerializer.Serialize(new { schemaVersion=1,
            source="Voice.dat WAV metadata; not native duration table", entries },
            new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
        Console.WriteLine($"[voice] {entries.Count} WAV metadata records; {entries.Count(e=>e.Error is not null)} errors; {Path.GetFullPath(output)}");
        return entries.Any(e=>e.Error is not null)?2:0;
    }

    private static int Play(byte[] bytes)
    {
        // Load from memory, no temp WAV extraction. Device lifetime belongs
        // only to this explicit one-shot review, never the duel loop.
        Raylib.InitAudioDevice();
        try
        {
            if(!Raylib.IsAudioDeviceReady())throw new InvalidDataException("Audio device unavailable.");
            var wave=Raylib.LoadWaveFromMemory(".wav",bytes);
            if(!Raylib.IsWaveValid(wave))throw new InvalidDataException("Raylib could not decode this WAV.");
            try
            {
                if(wave.SampleRate==0||wave.SampleCount==0)
                    throw new InvalidDataException("Decoded WAV has no playable frames.");
                // Limit the explicit review to short Internal resources.
                double seconds=(double)wave.SampleCount/wave.SampleRate;
                if(seconds>120)throw new InvalidDataException("Voice review is limited to 120 seconds.");
                var sound=Raylib.LoadSoundFromWave(wave);
                if(!Raylib.IsSoundValid(sound))throw new InvalidDataException("Could not create audio buffer.");
                try
                {
                    Raylib.PlaySound(sound);
                    var clock=Stopwatch.StartNew();
                    while(Raylib.IsSoundPlaying(sound))
                    {
                        if(clock.Elapsed.TotalSeconds>seconds+5)
                            throw new InvalidDataException("Voice review playback exceeded decoded duration.");
                        Thread.Sleep(10);
                    }
                    return 0;
                }
                finally { Raylib.StopSound(sound); Raylib.UnloadSound(sound); }
            }
            finally { Raylib.UnloadWave(wave); }
        }
        finally { Raylib.CloseAudioDevice(); }
    }
}
