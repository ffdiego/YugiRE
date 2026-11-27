using Joey.Core;
using Joey.LookAndFeel;
using System.Text;

static partial class Game
{
    static string? liveBreakShine;
    static readonly List<BreakDestruction> breakTails = [];
    static readonly PresentationTickClock breakTailClock = new();

    static void ConfigureLiveBreak(Archive archive)
    {
        liveBreakShine = null;
        try
        {
            var resource = NativeBreakResources.Parse(
                Encoding.Latin1.GetString(archive.Read(NativeBreakResources.Manifest)),
                archive.Entries.Select(e=>e.Name));
            var info = BitmapInfo.Read(archive.Read(resource));
            if (info.Width != NativeBreakResources.ShineWidth || info.Height != NativeBreakResources.ShineHeight)
                throw new InvalidDataException("Unexpected Break bitmap dimensions.");
            liveBreakShine = resource;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            Console.Error.WriteLine("[break] retaining explosion fallback: " + ex.Message);
        }
    }

    static BreakDestruction? TryCreateLiveBreak(DemoMonsterDestruction monster) =>
        TryCreateLiveBreak(monster.Cause, monster.Card,
            NativeLiveBreak.MonsterTarget(monster.Player,monster.Zone,monster.FaceDown,monster.Defense));

    static BreakDestruction? TryCreateLiveBreak(DemoSpellTrapDestruction removal) =>
        TryCreateLiveBreak(removal.Cause, removal.Card, NativeLiveBreak.FieldTarget(removal.Player,
            removal.Zone == SpellTrapTargetRules.FieldZone ? 10 : 5 + removal.Zone, removal.FaceDown));

    static BreakDestruction? TryCreateLiveBreak(RemovalCause cause, Card card, NativeBreakFieldTarget target)
    {
        // Default visual policy supported by the native producer split.
        // Individual exceptional routes can override later, with evidence.
        if (cause != RemovalCause.EffectDestruction || liveBreakShine is not { } shine) return null;
        bool hidden = (target.Descriptor & 0x8000) == 0;
        string? resource = hidden ? DatGraphics.MiniCardBack : graphics.Artwork.MiniFor(card);
        if (resource is null) return null;
        return new(card,target,ViewPlayer,resource,shine,new());
    }

    static void DrawPendingBreakCard(BreakDestruction broken)
    {
        var target = broken.Target;
        int zone = target.FieldZone;
        var cardZone = zone < 5 ? DuelCardZone.Monster : zone < 10 ? DuelCardZone.Spell : DuelCardZone.Field;
        DrawDuelCard(broken.Card,DuelCardLayout.OnBoard(cardZone,target.Side==broken.Perspective,zone%5,
            (target.Descriptor&0x4000)!=0,(target.Descriptor&0x8000)==0));
    }

    static void AdvanceBreakTails(TimeSpan elapsed)
    {
        if (breakTails.Count == 0) return;
        if (!presentation.Style.PlayAnimations)
        {
            breakTails.Clear();
            breakTailClock.Reset();
            return;
        }
        int updates = breakTailClock.Advance(elapsed,presentation.Style);
        foreach (var tail in breakTails)
            for (int i=0;i<updates && tail.Timeline.Active;i++)
                tail.Timeline.AdvanceNativeUpdate();
        breakTails.RemoveAll(tail=>!tail.Timeline.Active);
    }

    static void DrawLiveBreak(BreakDestruction broken)
    {
        var card = graphics.Texture(broken.CardPath);
        // Match the live board's orientation for the captured perspective.
        // Native break-bank orientation vs the original still requires a
        // paired capture (the isolated review exposes the raw bit convention).
        if (NativeBreakFieldReview.Sample(broken.Timeline,broken.Target.Center(broken.Perspective),
            broken.Target.Descriptor,broken.Perspective,card.Width,card.Height,effectCosine) is { } frame)
            DrawBreakFrame(frame,broken.CardPath,broken.ShinePath);
    }
}
