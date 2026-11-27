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

    static BreakDestruction? TryCreateLiveBreak(DemoMonsterDestruction monster)
    {
        if (liveBreakShine is not { } shine || !NativeLiveBreak.SupportsMonsterEffect(monster.EffectSourceResourceId))
            return null;
        // Never select artwork for a face-down snapshot. Capture the
        // perspective now; later turns and slot replacements cannot
        // change the departing card or its original anchor.
        string? resource = monster.FaceDown ? DatGraphics.MiniCardBack : graphics.Artwork.MiniFor(monster.Card);
        if (resource is null) return null;
        return new(monster,NativeLiveBreak.MonsterTarget(monster.Player,monster.Zone,
            monster.FaceDown,monster.Defense),ViewPlayer,resource,shine,new());
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
