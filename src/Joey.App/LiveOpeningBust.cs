using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Text;

static partial class Game
{
    static NativeOpeningBank? liveOpeningBank;
    static NativeOpeningPlayback? liveOpeningPlayback;
    static readonly PresentationTickClock liveOpeningClock = new();
    static bool liveOpeningTexturesReady, liveOpeningVoiceStarted;

    static void ConfigureLiveOpeningBust(Archive archive)
    {
        try
        {
            liveOpeningBank=NativeOpeningBank.Parse(
                Encoding.Latin1.GetString(archive.Read(NativeOpeningBank.Manifest)),
                archive.Entries.Select(e=>e.Name));
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            liveOpeningBank=null;
            Console.Error.WriteLine("[opening-bust] Skipping unsupported DAT bank: "+ex.Message);
        }
    }

    // Original opens before RPS. Alpha has no RPS screen; keep the existing
    // Duel Start/deal visual steps queued. No duel scheduler runs here.
    static void BeginLiveOpeningBust()
    {
        if(liveOpeningBank is null || !presentation.Style.PlayAnimations)return;
        int variant=NativeJoeyCinematicRouting.StartVariant((uint)Random.Shared.Next());
        liveOpeningPlayback=new(variant);
        liveOpeningTexturesReady=false;
        liveOpeningVoiceStarted=false;
        liveOpeningClock.Reset();
        screen="opening-bust";
    }

    static void ResetLiveOpeningBust()
    {
        if(liveOpeningPlayback is not null)duelAudio?.StopOpeningVoice();
        liveOpeningPlayback=null;
        liveOpeningTexturesReady=false;
        liveOpeningVoiceStarted=false;
        liveOpeningClock.Reset();
    }

    static void DrawLiveOpeningBust()
    {
        if(liveOpeningBank is not { } bank || liveOpeningPlayback is not { } playback)
        {
            ResetLiveOpeningBust();
            screen="duel";
            return;
        }
        // --alpha initializes before the GL context/audio device.
        try
        {
            if(!liveOpeningTexturesReady)
            {
                ValidateOpeningBustTextures(bank);
                liveOpeningTexturesReady=true;
            }
            if(liveOpeningClock.Advance(TimeSpan.FromSeconds(Raylib.GetFrameTime()),presentation.Style)>0)
            {
                bool audioComplete=!liveOpeningVoiceStarted || duelAudio?.OpeningVoiceComplete!=false;
                if(playback.AdvanceNativeUpdate(audioComplete) is uint request && presentation.Style.PlayJoeyVoice)
                    liveOpeningVoiceStarted=duelAudio?.PlayOpeningVoice(request)==true;
                // One Update/Draw per host frame, WAV at normal rate.
                DrawOpeningBustLayers(bank.Sample(playback.Counter,playback.Variant));
                playback.CompleteNativeDraw();
                if(!playback.Active)
                {
                    ResetLiveOpeningBust();
                    screen="duel";
                }
            }
            else DrawOpeningBustLayers(bank.Sample(playback.Counter,playback.Variant));
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            Console.Error.WriteLine("[opening-bust] Skipping unsupported presentation: "+ex.Message);
            ResetLiveOpeningBust();
            screen="duel";
        }
    }
}
