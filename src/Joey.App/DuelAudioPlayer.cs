using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Buffers.Binary;

// Optional playback; audio completion never controls the action queue.
internal sealed class DuelAudioPlayer : IDisposable
{
    readonly Archive archive;
    readonly Dictionary<string,Sound> effects=new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> failed=new(StringComparer.OrdinalIgnoreCase);
    Sound? music;
    string? musicPath;
    bool disposed;

    private DuelAudioPlayer(Archive archive) => this.archive=archive;

    internal static DuelAudioPlayer? Open(string? explicitPath,string? dataPath,bool disabled)
    {
        if(disabled)return null;
        Archive? source=null;
        bool device=false;
        try
        {
            string? path=explicitPath;
            if(path is null&&dataPath is not null)
            {
                string? dir=Path.GetDirectoryName(Path.GetFullPath(dataPath));
                if(dir is not null)
                    path=Directory.EnumerateFiles(dir).FirstOrDefault(p=>
                        Path.GetFileName(p).Equals("Voice.dat",StringComparison.OrdinalIgnoreCase));
            }
            if(string.IsNullOrWhiteSpace(path))
            {
                Console.Error.WriteLine("[audio] Voice.dat not found alongside data.dat; use --voice-dat=PATH.");
                return null;
            }
            source=new Archive(path);
            Raylib.InitAudioDevice();
            device=true;
            if(!Raylib.IsAudioDeviceReady())throw new InvalidDataException("Audio device unavailable.");
            Console.WriteLine("[audio] Voice.dat enabled: "+Path.GetFullPath(path));
            return new(source);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or DllNotFoundException or EntryPointNotFoundException)
        {
            if(device&&Raylib.IsAudioDeviceReady())Raylib.CloseAudioDevice();
            source?.Dispose();
            Console.Error.WriteLine("[audio] Disabled: "+ex.Message);
            return null;
        }
    }

    Sound? Load(string path)
    {
        if(failed.Contains(path))return null;
        try
        {
            byte[] bytes=archive.Read(path);
            _=NativeWaveInfo.ReadVoiceArchiveEntry(bytes,out int? canonicalEnd);
            if(canonicalEnd is int end)
            {
                Array.Resize(ref bytes,end);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),(uint)(end-8));
            }
            var wave=Raylib.LoadWaveFromMemory(".wav",bytes);
            if(!Raylib.IsWaveValid(wave))throw new InvalidDataException("Could not decode WAV.");
            try
            {
                if(wave.SampleRate==0||wave.SampleCount==0||
                    (double)wave.SampleCount/wave.SampleRate>120)
                    throw new InvalidDataException("Empty WAV or exceeds 120 second limit.");
                var sound=Raylib.LoadSoundFromWave(wave);
                if(!Raylib.IsSoundValid(sound))throw new InvalidDataException("Could not allocate sound.");
                return sound;
            }
            finally { Raylib.UnloadWave(wave); }
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or
            OverflowException or DllNotFoundException or EntryPointNotFoundException)
        {
            failed.Add(path);
            Console.Error.WriteLine("[audio] "+path+": "+ex.Message);
            return null;
        }
    }

    internal void Play(DuelSoundCue cue)
    {
        if(disposed)return;
        string path=DuelSoundAssets.Effect(cue);
        if(!effects.TryGetValue(path,out var sound))
        {
            if(Load(path) is not { } loaded)return;
            sound=loaded;
            effects.Add(path,sound);
        }
        Raylib.PlaySound(sound);
    }

    // First adapter: one bounded decoded track, repeat on completion.
    // No claim of native streaming, transitions or DirectSound volume.
    internal void UpdateMusic(string? path)
    {
        if(disposed)return;
        if(!string.Equals(path,musicPath,StringComparison.OrdinalIgnoreCase))
        {
            StopMusic();
            musicPath=path;
            if(path is not null&&Load(path) is { } loaded)
            {
                music=loaded;
                Raylib.SetSoundVolume(loaded,0.35f);
            }
        }
        if(music is { } current&&!Raylib.IsSoundPlaying(current))
            Raylib.PlaySound(current);
    }

    void StopMusic()
    {
        if(music is not { } current)return;
        Raylib.StopSound(current);Raylib.UnloadSound(current);
        music=null;
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        StopMusic();
        foreach(var sound in effects.Values)
        {
            Raylib.StopSound(sound);Raylib.UnloadSound(sound);
        }
        effects.Clear();
        archive.Dispose();
        Raylib.CloseAudioDevice();
    }
}

static partial class Game
{
    static DuelAudioPlayer? duelAudio;
    static void PlayDuelSound(DuelSoundCue cue) => duelAudio?.Play(cue);
}
