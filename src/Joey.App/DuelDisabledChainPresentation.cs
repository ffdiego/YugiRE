using Joey.Core;
using Joey.LookAndFeel;
using System.Text;

static partial class Game
{
    static string[] liveDisabledChainResources=[];

    static void ConfigureLiveDisabledChain(Archive archive)
    {
        liveDisabledChainResources=[];
        try
        {
            liveDisabledChainResources=NativeDisabledChainResources.Parse(
                Encoding.Latin1.GetString(archive.Read(NativeDisabledChainResources.Manifest)),
                archive.Entries.Select(entry=>entry.Name));
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException)
        { Console.Error.WriteLine("[disabled-chain] "+ex.Message); }
    }

    static void QueueJinzoSuppression(Card card,int player,int zone)
    {
        if(!presentation.Style.PlayAnimations || liveDisabledChainResources.Length!=6)return;
        var targets=duel.JinzoSuppressionPresentationTargets(player,zone,card);
        if(targets.Count==0)return;
        try
        {
            foreach(var resource in liveDisabledChainResources)
            {
                var texture=graphics.Texture(resource);
                if(texture.Width!=50 || texture.Height!=72)
                    throw new InvalidDataException("Unexpected disabled-chain dimensions: "+resource);
            }
            // The native 0059CCF0 sweep emits B1 in side/slot order.
            // 00408730 awaits active objects before the next B1; keep
            // these as substeps of this committed summon's existing scope.
            foreach(var trap in targets)
            {
                var target=NativeDisabledChainTarget.FromField(trap.Target.Side,5+trap.Slot,false);
                QueueCardEffect(new DisabledChainVisual(trap,target,ViewPlayer),NativeDisabledChainTimeline.Updates);
            }
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            liveDisabledChainResources=[];
            Console.Error.WriteLine("[disabled-chain] unsupported bank: "+ex.Message);
        }
    }

    static void DrawLiveDisabledChain(DisabledChainVisual visual,int updates)
    {
        if(!duel.IsCurrentSuppressionPresentationTarget(visual.Trap))return;
        var frame=NativeDisabledChainFrame.FromCounter(updates,visual.Target.Descriptor);
        DrawDisabledChainIndicator(visual.Target.Center(visual.Perspective),frame,liveDisabledChainResources);
    }
}
