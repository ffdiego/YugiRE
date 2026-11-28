using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;

static partial class Game
{
    static NativeCoinResource[] liveCoinResources=[];
    static NativeDiceResource[] liveDiceResources=[];

    static void ConfigureLiveRandomBanks(Archive archive)
    {
        liveCoinResources=[];
        liveDiceResources=[];
        try
        {
            liveCoinResources=NativeCoinResources.Parse(
                Encoding.Latin1.GetString(archive.Read(NativeCoinResources.Manifest)),archive.Entries.Select(e=>e.Name));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException)
        { Console.Error.WriteLine("[coin] "+ex.Message); }
        try
        {
            var bank=NativeDiceResources.Parse(
                Encoding.Latin1.GetString(archive.Read(NativeDiceResources.Manifest)),archive.Entries.Select(e=>e.Name));
            if(bank.Length!=13) throw new InvalidDataException("Unexpected dice bank size.");
            liveDiceResources=bank;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException)
        { Console.Error.WriteLine("[dice] "+ex.Message); }
    }

    static CardPoint RandomBankOrigin(int side,int region) =>
        DuelCardLayout.Center(region<5 ? DuelCardZone.Monster : region<10 ? DuelCardZone.Spell : DuelCardZone.Field,
            side==ViewPlayer,region%5);

    static void QueueLiveCoin(LabCoinResult result)
    {
        if(!presentation.Style.PlayAnimations || liveCoinResources.Length!=6) return;
        try
        {
            var sizes=liveCoinResources.Select(r=>{
                var t=graphics.Texture(r.Path);
                return NativeCoinResources.ValidateTexture(r,t.Width,t.Height);
            }).ToArray();
            var timeline=new NativeCoinTimeline(result.Count,result.Bits,RandomBankOrigin(result.Side,result.Region));
            QueueCardEffect(new CoinResultVisual(timeline,result.Highlight,sizes),timeline.Updates);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException)
        { Console.Error.WriteLine("[coin] presentation skipped: "+ex.Message); }
    }

    static void QueueLiveDice(int side, int region, int face)
    {
        if(!presentation.Style.PlayAnimations || liveDiceResources.Length!=13) return;
        try
        {
            var sizes=liveDiceResources.Select((r,k)=>{
                var t=DiceBankTexture(r.Path);
                return NativeDiceResources.ValidateTexture(k,r,t.Width,t.Height);
            }).ToArray();
            var timeline=new NativeDiceTimeline(face, RandomBankOrigin(side, region));
            QueueCardEffect(new DiceResultVisual(timeline,sizes),NativeDiceTimeline.Updates);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException)
        { Console.Error.WriteLine("[dice] presentation skipped: "+ex.Message); }
    }

    static void DrawLiveCoin(CoinResultVisual visual)
    {
        var timeline=visual.Timeline;
        if(!timeline.Active)return;
        for(int i=0;i<timeline.Count;i++)
        {
            var frame=NativeCoinPassLayout.Sample(timeline,i,visual.Highlight,effectCosine,k=>visual.Textures[k]);
            var extras=NativeCoinExtraLayout.Sample(timeline,i,frame,k=>visual.Textures[k]);
            // Reflection plan is known, its native compositor is not.
            foreach(var pass in frame.Passes.Concat(extras.Sparkles))
                DrawRandomBankPass(graphics.Texture(liveCoinResources[pass.Key].Path), pass.Source, pass.Destination, pass.Parameter256);
        }
    }

    static void DrawLiveDice(DiceResultVisual visual)
    {
        // Render a cached sample on repeated host frames or while paused.
        // A new native step alone authorizes another state-mutating Draw.
        if(visual.Frame is not { } frame || frame.Counter!=visual.Timeline.Counter)
        {
            visual.Frame=visual.Timeline.Draw(effectCosine,k=>visual.Textures[k]);
            if(visual.Frame?.SoundRequest is not null) PlayDuelSound(DuelSoundCue.DiceBounce);
        }
        if(visual.Frame is not { } sample)return;
        DrawRandomBankPass(DiceBankTexture(liveDiceResources[sample.Shadow.Key].Path),
            sample.Shadow.Source, sample.Shadow.Destination, sample.Shadow.Intensity);
        DrawRandomBankPass(DiceBankTexture(liveDiceResources[sample.Die.Key].Path),
            sample.Die.Source, sample.Die.Destination, sample.Die.Intensity);
    }

    static Texture2D DiceBankTexture(string path) => NativeDiceResources.IsDiceBitmap(path)
        ? graphics.CornerKeyedBitmapTexture(path)
        : graphics.Texture(path);

    static void DrawRandomBankPass(Texture2D texture, CardBounds source, CardBounds destination, int intensity)
    {
        if(destination.Width<=0 || destination.Height<=0)return;
        Raylib.DrawTexturePro(texture,ToRectangle(source),ToRectangle(destination),Vector2.Zero,0,
            new Color(255,255,255,Math.Clamp(intensity,0,255)));
    }
}
