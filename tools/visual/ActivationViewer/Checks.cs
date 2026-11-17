using System.Text.Json;
namespace ActivationViewer;
public static class Checks
{
    public static int Run()
    {
        int count=0;
        void Check(bool ok,string message) { if(!ok) throw new Exception(message);count++; }
        int[] table=Effect.Cosines();
        Check(table[0]==65536 && table[128]==0 && table[256]==-65536 && table[384]==0,"Cosine quarter turns");
        Check(table[64]==46340,"16.16 diagonal cosine truncation");
        Check(Effect.MulDiv(163,32768)==82 && Effect.MulDiv(-163,32768)==-82,"MulDiv half away from zero");
        Check(Effect.Quad(164,164,0,0,0,0,table).Length==0,"Zero scale is a no-op");
        Check(Effect.Quad(164,164,525,352,0,65536,table).SequenceEqual(new Point[]{new(444,271),new(607,271),new(607,434),new(444,434)}),"Width-minus-one native bounds");
        Check(Effect.Quad(164,164,525,352,128,65536,table).SequenceEqual(new Point[]{new(606,271),new(606,434),new(443,434),new(443,271)}),"Integer clockwise quarter-turn");
        Check(Effect.Blend(240,0,128,128)==180,"Two LUT divisions, descending channel");
        Check(Effect.Blend(0,240,128,128)==60,"Two LUT divisions, ascending channel");
        Check(Effect.Blend(255,0,255,128)==127,"Black scratch opacity 128/255");
        Check(Effect.Blend(64,255,0,255)==64 && Effect.Blend(64,255,255,255)==255,"Alpha endpoints");
        // A hand-specified RGB pattern must survive a full-size axis-aligned polygon byte-for-byte.
        byte[] pattern=new byte[36];for(int i=0;i<9;i++) { pattern[i*4]=(byte)(20+i);pattern[i*4+1]=40;pattern[i*4+2]=60;pattern[i*4+3]=255; }
        var raster=new Raster(3,3,pattern);byte[] destination=new byte[36];
        Effect.Polygon(destination,3,3,raster,Effect.Quad(3,3,1,1,0,65536,table),255);
        Check(destination.SequenceEqual(pattern),"Raster preserves every opaque texel and inclusive endpoints");
        byte[] background=Effect.Checkerboard();
        Check(Effect.Render(new(0,400,300,54,78),raster,raster,background,table).SequenceEqual(background),"Initial frame unchanged");
        Check(Effect.Render(new(256,400,300,54,78),raster,raster,background,table).SequenceEqual(background),"Final zero-amplitude frame unchanged");
        Check(Effect.Render(new(128,525,600,54,78),raster,raster,background,table)[(580*800+525)*4]==19,"Off-canvas hand center clips scratch and darkens visible checker texel from 38 to 19");
        try { Effect.Render(new(272,400,300,54,78),raster,raster,background,table);throw new Exception("Accepted dead effect"); }
        catch(InvalidDataException) {count++;}
        Console.WriteLine($"{count} synthetic checks passed; no native execution or pixel-fidelity claim.");
        return 0;
    }
}
public sealed record Replay(int[] Table,List<EffectFrame> Frames,List<Point[][]> CapturedQuads)
{
    public static Replay Read(string path)
    {
        if(new FileInfo(path).Length>8*1024*1024) throw new InvalidDataException("Oversized replay");
        using var json=JsonDocument.Parse(File.ReadAllText(path));var root=json.RootElement;
        if(root.GetProperty("schema").GetString()!="joey-activation-replay-v1" ||
           root.GetProperty("exeSha256").GetString()!="db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de" ||
           !root.GetProperty("nativeExecutionDeclared").GetBoolean() ||
           root.GetProperty("datSha256").GetString()!="7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed") throw new InvalidDataException("Unrecognized capture provenance");
        int[] table=root.GetProperty("cosineTable").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        if(table.Length!=512 || table.Any(x=>Math.Abs((long)x)>65536)) throw new InvalidDataException("Invalid captured cosine table");
        List<EffectFrame> frames=[];List<Point[][]> quads=[];double prior=-1;
        foreach(var f in root.GetProperty("frames").EnumerateArray())
        {
            double seconds=f.GetProperty("seconds").GetDouble();
            if(!double.IsFinite(seconds) || seconds<0 || seconds<prior) throw new InvalidDataException("Invalid measured timeline");prior=seconds;
            frames.Add(new(f.GetProperty("counter").GetInt32(),f.GetProperty("x").GetInt32(),f.GetProperty("y").GetInt32(),
                          f.GetProperty("cardWidth").GetInt32(),f.GetProperty("cardHeight").GetInt32(),seconds));
            quads.Add(f.GetProperty("polygons").EnumerateArray().Select(p=>
            {
                int[] xy=p.GetProperty("xy").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
                if(xy.Length!=8) throw new InvalidDataException("Invalid captured polygon");
                return Enumerable.Range(0,4).Select(i=>new Point(xy[i*2],xy[i*2+1])).ToArray();
            }).ToArray());
        }
        if(frames.Count is <1 or >1024) throw new InvalidDataException("Invalid frame count");
        return new(table,frames,quads);
    }
}
