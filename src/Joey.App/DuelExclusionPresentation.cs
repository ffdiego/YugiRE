using Joey.Core;
using Joey.LookAndFeel;
using System.Text;

static partial class Game
{
    sealed class ExclusionVisual(NativeExclusionTimeline timeline,string cardPath,CardPoint center,int perspective,
        int width,int height)
    {
        public NativeExclusionTimeline Timeline {get;}=timeline;
        public string CardPath {get;}=cardPath;
        public NativeExclusionDraw? Frame {get;private set;}
        public void NextDraw() => Frame=Timeline.Draw(center,width,height,perspective,effectCosine,
            NativeExclusionResources.ExpectedSize);
    }

    static string[] liveExclusionResources=[];
    static readonly List<ExclusionVisual> liveExclusions=[];
    static readonly PresentationTickClock exclusionClock=new();

    static void ConfigureLiveExclusion(Archive archive)
    {
        liveExclusionResources=[];
        try
        {
            liveExclusionResources=NativeExclusionResources.Parse(
                Encoding.Latin1.GetString(archive.Read(NativeExclusionStart.Manifest)),
                archive.Entries.Select(e=>e.Name));
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException)
        { Console.Error.WriteLine("[exclusion] "+ex.Message); }
    }

    static void QueueLiveExclusion(DemoBanishment movement)
    {
        if(!presentation.Style.PlayAnimations || liveExclusionResources.Length!=8)return;
        try
        {
            // The Codex located the immediate producer in zone 0F. Treat
            // the real BanishCommand as an inferred destination route;
            // don't conflate it with destruction or fusion material.
            for(int k=0;k<liveExclusionResources.Length;k++)
            {
                var t=graphics.Texture(liveExclusionResources[k]);
                if(new CardBounds(0,0,t.Width,t.Height)!=NativeExclusionResources.ExpectedSize(k))
                    throw new InvalidDataException("Unexpected exclusion resource geometry.");
            }
            var visible=movement.VisibleCard;
            string cardPath=visible is null?DatGraphics.MiniCardBack:
                graphics.Artwork.MiniFor(visible)??DatGraphics.MiniCardBack;
            var card=graphics.Texture(cardPath);
            var start=NativeExclusionDestination.Start(movement.Side,visible is not null,
                visible is null?0u:(uint)visible.Index);
            var visual=new ExclusionVisual(new(start),cardPath,
                NativeExclusionDestination.Center(movement.Side==ViewPlayer),ViewPlayer,card.Width,card.Height);
            // The command does NOT own this lifetime: native virtual +4
            // always false. Prime the first frame; subsequent draws follow
            // the independent presentation clock, including while busy.
            if(liveExclusions.Count==0)exclusionClock.Reset();
            visual.NextDraw();
            liveExclusions.Add(visual);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            liveExclusions.Clear();liveExclusionResources=[];
            Console.Error.WriteLine("[exclusion] presentation unavailable: "+ex.Message);
        }
    }

    static void AdvanceLiveExclusions(TimeSpan elapsed)
    {
        if(!presentation.Style.PlayAnimations)
        {
            liveExclusions.Clear();exclusionClock.Reset();
            return;
        }
        if(liveExclusions.Count==0 || exclusionClock.Advance(elapsed,presentation.Style)==0)return;
        // One native Draw per presentation step. A terminal sample remains
        // visible until the next step, and is not consumed by a redraw.
        liveExclusions.RemoveAll(effect=>!effect.Timeline.Active);
        foreach(var visual in liveExclusions)visual.NextDraw();
    }

    static void DrawLiveExclusions()
    {
        foreach(var effect in liveExclusions)
            if(effect.Frame is {} frame)
                foreach(var pass in frame.Passes)
                    DrawAltExTexture(pass.ResourceKey is int k ? liveExclusionResources[k] : effect.CardPath,
                        pass.Bounds,pass.Intensity,pass.RotationDegrees);
    }
}
