using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;

static partial class Game
{
    static NativePhaseNoticeResource[] turnNoticeBank=[];
    static bool turnNoticeResourceFailed;

    static void ConfigureTurnNotice(Archive archive)
    {
        turnNoticeBank=[];
        turnNoticeResourceFailed=false;
        try
        {
            var bank=NativePhaseNoticeResources.Parse(
                Encoding.Latin1.GetString(archive.Read(NativePhaseNoticeResources.Manifest)),
                archive.Entries.Select(e=>e.Name));
            NativePhaseNoticeResources.ValidateTurnNoticeBank(bank);
            turnNoticeBank=bank;
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException)
        {
            Console.Error.WriteLine("[turn-notice] "+ex.Message);
        }
    }

    static void DrawTurnNotice(NativeTurnNoticePlan plan,int elapsed)
    {
        if(turnNoticeBank.Length==0||turnNoticeResourceFailed||
            plan.Sample(elapsed).TurnNoticeCounter is not int counter||
            plan.DrawAtCounter(counter) is not { } draw)return;
        try
        {
            var texture=graphics.Texture(turnNoticeBank[draw.LookupKey].Path);
            if(texture.Width!=395||texture.Height!=65)
                throw new InvalidDataException("Unexpected turn notice texture size.");
            var q=NativeNoticeProjection.Project(draw,texture.Width,texture.Height,effectCosine);
            if(q.Degenerate)return;
            if(draw.AngleIndex==0)
            {
                Raylib.DrawTexture(texture,q.Destination.TopLeft.X,q.Destination.TopLeft.Y,Color.White);
                return;
            }
            // Approximate native quad with point-filtered vertical strips.
            // No claim of exact fixed-point raster or blend.
            var d=q.Destination;
            int width=d.TopRight.X-d.TopLeft.X+1;
            for(int i=0;i<width;i++)
            {
                float t=i/(float)(width-1);
                int sourceX=(int)MathF.Round(t*(texture.Width-1));
                int top=(int)MathF.Round(d.TopLeft.Y+t*(d.TopRight.Y-d.TopLeft.Y));
                int bottom=(int)MathF.Round(d.BottomLeft.Y+t*(d.BottomRight.Y-d.BottomLeft.Y));
                if(bottom<top)continue;
                Raylib.DrawTexturePro(texture,new Rectangle(sourceX,0,1,texture.Height),
                    new Rectangle(d.TopLeft.X+i,top,1,bottom-top+1),Vector2.Zero,0,Color.White);
            }
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException)
        {
            turnNoticeResourceFailed=true;
            Console.Error.WriteLine("[turn-notice] Keeping existing panel flip: "+ex.Message);
        }
    }
}
