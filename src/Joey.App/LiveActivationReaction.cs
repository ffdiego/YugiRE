using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Text;

static partial class Game
{
    static NativeReaction07? liveReactionBank;
    static NativeReactionPlayback? liveActivationReaction;
    static readonly PresentationTickClock liveReactionClock = new();
    static bool liveActivationVoiceStarted;
    static uint liveReactionScene;

    static NativeReaction07 ReadActivationReaction(Archive archive) =>
        NativeReaction07.Parse(Encoding.Latin1.GetString(archive.Read(NativeReaction07.Manifest)),
            archive.Entries.Select(entry => entry.Name));

    static void ConfigureLiveActivationReaction(Archive archive)
    {
        try
        {
            liveReactionBank = ReadActivationReaction(archive);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            liveReactionBank = null;
            Console.Error.WriteLine("[reaction07] Skipping unsupported DAT bank: " + ex.Message);
        }
    }

    static void BeginLiveActivationReaction(DuelActionPresentation action)
    {
        if (!presentation.Style.PlayAnimations || liveReactionBank is not { } bank ||
            alphaCpu?.Player != action.Source.Side || action.Source.Zone != DuelZone.Hand ||
            !NativeReaction07.IsGenericSpellActivation(action.Card.Id, action.Card.ClassCode, action.Card.Properties))
            return;

        try
        {
            ValidateActivationReactionTextures(bank);
            liveActivationReaction = new(NativeReaction07.ObservedPrimarySound);
            liveReactionClock.Reset();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            liveReactionBank = null;
            Console.Error.WriteLine("[reaction07] Skipping unsupported presentation: " + ex.Message);
        }
    }

    static bool AdvanceLiveActivationReaction(TimeSpan elapsed)
    {
        if (liveActivationReaction is not { } playback) return false;
        if (liveActivationVoiceStarted) duelAudio?.SetOpeningVoicePaused(visualReviewPaused);
        if (liveReactionClock.Advance(elapsed, presentation.Style) == 0) return true;

        bool complete = !liveActivationVoiceStarted || duelAudio?.OpeningVoiceComplete != false;
        if (playback.Step(complete).RequestedSound is not null && presentation.Style.PlayJoeyVoice)
        {
            liveActivationVoiceStarted = duelAudio?.PlayActivationReactionVoice() == true;
            if (liveActivationVoiceStarted) duelAudio?.SetOpeningVoicePaused(visualReviewPaused);
        }
        // The existing activation remains busy until the reaction's terminal Draw.
        return true;
    }

    static void DrawLiveActivationReaction()
    {
        if (liveActivationReaction is not { Counter: > 0 } playback) return;
        DrawActivationReactionLayers(liveReactionBank!, liveReactionScene);
        if (!playback.Active) StopLiveActivationReaction();
    }

    static void StopLiveActivationReaction()
    {
        if (liveActivationVoiceStarted) duelAudio?.StopOpeningVoice();
        liveActivationVoiceStarted = false;
        liveActivationReaction = null;
        liveReactionClock.Reset();
    }

    static void ValidateActivationReactionTextures(NativeReaction07 bank)
    {
        foreach (string resource in bank.Resources) _ = graphics.Texture(resource);
        var basis = graphics.Texture(bank.BaseResource);
        if (basis.Width != NativeReaction07.Width || basis.Height != NativeReaction07.Height)
            throw new InvalidDataException("Unexpected reaction07 base bitmap dimensions.");
    }

    static void DrawActivationReactionLayers(NativeReaction07 bank, uint scene)
    {
        Raylib.DrawTexture(graphics.Texture(bank.BaseResource), bank.Origin.X, bank.Origin.Y, Color.White);
        foreach (var layer in bank.Sample(scene))
            Raylib.DrawTexture(graphics.Texture(layer.Resource), layer.Position.X, layer.Position.Y, Color.White);
    }
}
