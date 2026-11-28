using System.Globalization;

namespace Joey.LookAndFeel;

/// <summary>00416050/00416260: result playback is distinct from opening.
/// The caller supplies the raw native result code, never a local win enum.</summary>
public sealed class NativeResultPlayback
{
    public const int VoiceCounter=96, FirstPollCounter=484, ExitStart=0x10000, ExitEnd=0x10080;
    public int Counter { get; private set; }
    public bool Active { get; private set; }=true;
    public bool VoiceRequested { get; private set; }
    public uint VoiceRequest { get; }

    // Roteiro 72: seven descriptors point to Joey_53 through Joey_59.
    public static string VoicePath(uint request)
    {
        if(request is <0x10000059u or >0x1000005fu)
            throw new ArgumentOutOfRangeException(nameof(request));
        return DuelSoundAssets.InternalPrefix+"joey_"+(53+request-0x10000059u).ToString("00",CultureInfo.InvariantCulture)+".wav";
    }
    public NativeResultPlayback(int nativeResult,int variant)
    {
        if(variant<0 || variant>=(nativeResult==3?1:3))
            throw new ArgumentOutOfRangeException(nameof(variant));
        VoiceRequest=NativeJoeyCinematicRouting.Result(nativeResult).Sound+(uint)variant;
    }

    public uint? AdvanceNativeUpdate(bool audioComplete)
    {
        if(!Active || Counter>=ExitEnd)return null;
        uint? request=null;
        if(Counter==VoiceCounter)
        {
            VoiceRequested=true;
            request=VoiceRequest;
        }
        if(Counter>=FirstPollCounter && Counter<ExitStart && audioComplete)
            Counter=ExitStart;
        // Unlike opening, the successful audio branch also adds four.
        Counter=checked(Counter+4);
        return request;
    }

    public void CompleteNativeDraw()
    {
        if(Counter>=ExitEnd)Active=false;
    }
}

public readonly record struct NativeResultResource(int Key,string Path,int Parameter,int Width,int Height)
{
    public CardPoint Position => new(Parameter/1000,Parameter%1000);
    public bool CornerKey => Key>=3;
}
public readonly record struct NativeResultLayer(int Key,string Path,CardPoint Position,bool CornerKey);

/// <summary>English result bank, manifest order and observed dimensions.
/// Temporary surface composition is represented by ordered diagnostic layers.
/// Facial selector 005DA7E0 recovered in collection 72.</summary>
public sealed class NativeResultBank
{
    public string Manifest { get; }
    public int NativeResult { get; }
    public int VariantCount => resources[0]!.Value.Parameter%1000;
    private readonly NativeResultResource?[] resources;
    private NativeResultBank(int result,NativeResultResource?[] bank)
    {
        NativeResult=result;
        Manifest=NativeJoeyCinematicRouting.Result(result).Manifest;
        resources=bank;
    }
    public IEnumerable<NativeResultResource> Resources => resources.OfType<NativeResultResource>();

    public static NativeResultBank Parse(int nativeResult,string text,IEnumerable<string> archiveNames)
    {
        string[] paths;
        int[] parameters,widths,heights;
        if(nativeResult==2)
        {
            paths=["make_bg#.bmp","make_kage.yga","make_?_moji.yga","reaction14.bmp",
                "reaction14_eyes2.bmp","reaction14_eyes1.bmp","reaction14_kuti2.bmp","reaction14_kuti1.bmp",
                "fukidashi/?_make01.bmp","fukidashi/?_make02.bmp","fukidashi/?_make03.bmp"];
            parameters=[1003,438047,3,401043,120072,120072,152117,152117,43039,43039,43039];
            widths=[800,362,341,399,78,78,40,40,417,417,417];
            heights=[600,553,280,557,41,41,28,28,224,224,224];
        }
        else if(nativeResult==3)
        {
            paths=["doro_bg#.bmp","doro_kage.yga","doro_?_moji.yga","reaction15.bmp",
                "null.bmp","null.bmp","reaction15_kuti1.bmp","reaction15_kuti2.bmp","fukidashi/?_doro01.bmp"];
            parameters=[1,50,4,40,0,0,190154,190154,330064];
            widths=[800,369,457,366,0,0,43,43,417];
            heights=[600,550,174,560,0,0,39,39,224];
        }
        else
        {
            paths=["kati_bg#.bmp","kati_kage.yga","kati_?_moji.yga","reaction09.bmp",
                "null.bmp","null.bmp","reaction09_kuti1.bmp","null.bmp",
                "fukidashi/?_kati01.bmp","fukidashi/?_kati02.bmp","fukidashi/?_kati03.bmp"];
            parameters=[3,42,4,40,0,0,213091,0,344041,344041,344041];
            widths=[800,389,352,355,0,0,70,0,417,417,417];
            heights=[600,558,292,560,0,0,80,0,224,224,224];
        }
        var rows=text.Split('\n').Select(l=>l.Split("//",2)[0]
            .Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))
            .Where(l=>l.Length>0).ToArray();
        if(rows.Length!=paths.Length)throw new InvalidDataException("Unexpected result bank size.");
        var names=new HashSet<string>(archiveNames,StringComparer.OrdinalIgnoreCase);
        var bank=new NativeResultResource?[paths.Length];
        for(int key=0;key<paths.Length;key++)
        {
            var row=rows[key];
            bool isNull=paths[key]=="null.bmp";
            if(row.Length<1 || row.Length>2 ||
                !row[0].Equals(isNull?"null.bmp":"win/"+paths[key],StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unexpected result row {key}.");
            int parameter=0;
            if((row.Length==2 && !int.TryParse(row[1],NumberStyles.None,CultureInfo.InvariantCulture,out parameter))
                || (row.Length==1 && !isNull) || parameter!=parameters[key])
                throw new InvalidDataException($"Unexpected result parameter {key}.");
            if(isNull)continue;
            string path=NativeJoeyCinematicRouting.Prefix+@"win\"+paths[key].Replace('?','e').Replace('/','\\');
            if(!names.Contains(path))throw new InvalidDataException("Missing result resource: "+path);
            bank[key]=new(key,path,parameter,widths[key],heights[key]);
        }
        return new(nativeResult,bank);
    }

    public int CharacterX(int counter)
    {
        var sprite=resources[3]!.Value;
        int travel=(Math.Clamp(counter-64,0,32)*sprite.Width)>>5;
        bool right=(resources[0]!.Value.Parameter/1000&1)!=0;
        return sprite.Position.X+(right?sprite.Width-travel:travel-sprite.Width);
    }

    // 004165AA..00416669: corner selected by manifest parameter, then
    // horizontal displacement from the captured 512-entry cosine table.
    public CardPoint BannerPosition(int counter,IReadOnlyList<int> cosine)
    {
        if(cosine.Count!=512)throw new ArgumentException("Expected native cosine table.",nameof(cosine));
        var banner=resources[2]!.Value;
        int x=(banner.Parameter==2 || banner.Parameter==4)?800-banner.Width:0;
        int y=banner.Parameter>=3?600-banner.Height:0;
        int phase=Math.Clamp(counter-96,0,64);
        int travel=(cosine[(phase*2-128)&511]*banner.Width)>>16;
        return new(x+(x+banner.Width/2<400?travel-banner.Width:banner.Width-travel),y);
    }

    // 005DA7E0: signed dwords -1,0,1,0. Phase zero branches away.
    public static int FaceOffset(int counter) => ((counter>>3)&3) switch
    {
        0=>-1, 2=>1, _=>0
    };

    public NativeResultLayer[] Sample(int counter,int variant,IReadOnlyList<int> cosine)
    {
        if(counter<0 || counter>NativeResultPlayback.ExitEnd || variant<0 || variant>=VariantCount)
            throw new ArgumentOutOfRangeException();
        var layers=new List<NativeResultLayer>();
        void Add(int key,CardPoint? position=null)
        {
            if(resources[key] is { } r)
                layers.Add(new(key,r.Path,position??r.Position,r.CornerKey));
        }
        Add(0,new(0,0));
        // 00416050 composes shadow and dialogue on INPUT 96; their
        // first post-update Draw is 100. Character is drawn separately.
        if(counter>96){Add(1);Add(8+variant);}
        if(counter>=64)
        {
            int x=CharacterX(counter);
            Add(3,new(x,resources[3]!.Value.Position.Y));
            int offset=FaceOffset(counter);
            if(counter>=96 && offset>=0)
            {
                void Overlay(int key)
                {
                    if(resources[key] is { } r && r.Parameter!=0)
                        Add(key,new(x+r.Position.X,resources[3]!.Value.Position.Y+r.Position.Y));
                }
                if(((counter>>5)&7)==0)Overlay(4+offset);
                if(counter<NativeResultPlayback.ExitStart)Overlay(6+offset);
            }
        }
        if(counter>96)Add(2,BannerPosition(counter,cosine));
        return layers.ToArray();
    }
}
