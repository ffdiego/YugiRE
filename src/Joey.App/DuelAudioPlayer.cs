using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Runtime.InteropServices;

// Optional playback; audio completion never controls the action queue.
internal sealed class DuelAudioPlayer : IDisposable
{
    readonly Archive archive;
    readonly Dictionary<string,Sound> effects=new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> failed=new(StringComparer.OrdinalIgnoreCase);
    Music? music;
    GCHandle musicBytes;
    string? musicPath;
    bool disposed;
    Sound? openingVoice;
    bool openingVoicePaused;

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
            byte[] bytes=NativeWaveInfo.PrepareVoiceArchiveEntry(archive.Read(path),out _);
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

    // One owned voice playback for V31 busts. The cache owns the Sound;
    // reset/exit stops it without unloading or affecting music.
    internal bool PlayOpeningVoice(uint request) => PlayBustVoice(NativeOpeningPlayback.VoicePath(request));
    internal bool PlayResultVoice(uint request) => PlayBustVoice(NativeResultPlayback.VoicePath(request));

    private bool PlayBustVoice(string path)
    {
        StopOpeningVoice();
        if(disposed)return false;
        if(!effects.TryGetValue(path,out var sound))
        {
            if(Load(path) is not { } loaded)return false;
            sound=loaded;
            effects.Add(path,sound);
        }
        openingVoice=sound;
        Raylib.PlaySound(sound);
        return true;
    }

    internal bool OpeningVoiceComplete => openingVoice is not { } sound ||
        (!openingVoicePaused && !Raylib.IsSoundPlaying(sound));

    internal void SetOpeningVoicePaused(bool paused)
    {
        if(openingVoice is not { } sound || paused==openingVoicePaused)return;
        if(paused)
        {
            if(!Raylib.IsSoundPlaying(sound))return;
            Raylib.PauseSound(sound);
        }
        else Raylib.ResumeSound(sound);
        openingVoicePaused=paused;
    }

    internal void StopOpeningVoice()
    {
        if(openingVoice is { } sound)Raylib.StopSound(sound);
        openingVoice=null;
        openingVoicePaused=false;
    }

    // Keep the bounded WAV input in memory and let raylib feed decoded
    // frames to the device in blocks.
    internal void UpdateMusic(string? path)
    {
        if(disposed)return;
        if(!string.Equals(path,musicPath,StringComparison.OrdinalIgnoreCase))
        {
            StopMusic();
            musicPath=path;
            if(path is not null) music=LoadMusic(path);
        }
        if(music is { } current) Raylib.UpdateMusicStream(current);
    }

    Music? LoadMusic(string path)
    {
        if(failed.Contains(path)) return null;
        GCHandle buffer=default;
        Music stream=default;
        bool valid=false;
        try
        {
            byte[] bytes=NativeWaveInfo.PrepareVoiceArchiveEntry(archive.Read(path),out _);
            // raylib's decoder retains a pointer into the WAV for the life
            // of the stream, including across compacting collections.
            buffer=GCHandle.Alloc(bytes,GCHandleType.Pinned);
            stream=Raylib.LoadMusicStreamFromMemory(".wav",bytes);
            valid=Raylib.IsMusicValid(stream);
            if(!valid) throw new InvalidDataException("Could not decode music WAV.");
            stream.Looping=true;
            Raylib.SetMusicVolume(stream,0.35f);
            Raylib.PlayMusicStream(stream);
            Console.WriteLine($"[audio] Music stream: {path} ({Raylib.GetMusicTimeLength(stream):F1} s)");
            musicBytes=buffer;
            buffer=default;
            return stream;
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or
            OverflowException or DllNotFoundException or EntryPointNotFoundException)
        {
            failed.Add(path);
            Console.Error.WriteLine("[audio] "+path+": "+ex.Message);
            return null;
        }
        finally
        {
            if(buffer.IsAllocated)
            {
                try { if(valid) Raylib.UnloadMusicStream(stream); }
                finally { buffer.Free(); }
            }
        }
    }

    void StopMusic()
    {
        if(music is not { } current)return;
        music=null;
        try
        {
            Raylib.StopMusicStream(current);
            Raylib.UnloadMusicStream(current);
        }
        finally { musicBytes.Free(); }
    }

    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        StopOpeningVoice();
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
