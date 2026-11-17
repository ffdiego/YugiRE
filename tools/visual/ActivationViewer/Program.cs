using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ActivationViewer;
using Joey.Core;
using Raylib_cs;
using RColor=Raylib_cs.Color;

return MainProgram(args);
static int MainProgram(string[] args)
{
    try
    {
        if(args.Length==0 || args.Contains("--help"))
        {
            Console.WriteLine("ActivationViewer --dat DAT --card-size W,H [--counter 128] [--position X,Y] [--export-dir scratch/path --headless]\n"+
                "  --replay validated-replay.json: measured draw counters/timestamps and scratch dimensions\n"+
                "  --background before.png --reference original.png --reference-counter N (800x600)\n"+
                "  --self-test: synthetic checks without DAT/GPU/window\n"+
                "GUI: arrows step, Space play captured timestamps, R restart, D difference, Escape close.\n"+
                "Manual mode has no automatic clock. No Windows capture has been validated yet.");return 0;
        }
        if(args.SequenceEqual(new[]{"--self-test"})) return Checks.Run();
        var flags=new HashSet<string>{"--headless","--check"};
        var valid=new HashSet<string>{"--dat","--card-size","--counter","--position","--export-dir","--replay","--background","--reference","--reference-counter"};
        var options=new Dictionary<string,string>();
        for(int i=0;i<args.Length;i++)
        {
            string key=args[i];if(flags.Contains(key)) {options.Add(key,"true");continue;}
            if(!valid.Contains(key) || i+1==args.Length) throw new ArgumentException("Unknown/missing option: "+key);
            options.Add(key,args[++i]);
        }
        if(!options.TryGetValue("--dat",out var datPath)) throw new ArgumentException("--dat required");
        if(options.ContainsKey("--reference") && !options.ContainsKey("--reference-counter")) throw new ArgumentException("Declare --reference-counter; no automatic alignment is assumed");
        using(var dataStream=File.OpenRead(datPath))
            if(Convert.ToHexString(SHA256.HashData(dataStream)).ToLowerInvariant()!="7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed")
                throw new InvalidDataException("Original DAT fingerprint mismatch");
        using var archive=new Archive(datPath);
        var lower=Raster.Yga(archive.Read(@"j\duel\effect\kouka_sita_b.yga"));
        var upper=Raster.Yga(archive.Read(@"j\duel\effect\kouka_ue_b.yga"));
        if(lower.Width!=164 || lower.Height!=164 || upper.Width!=164 || upper.Height!=164) throw new InvalidDataException("Unexpected activation assets");
        var replay=options.TryGetValue("--replay",out var replayPath)?Replay.Read(replayPath):null;
        int[] table=replay?.Table??Effect.Cosines();
        var position=ParsePair(options.GetValueOrDefault("--position","525,352"));
        List<EffectFrame> frames;
        if(replay!=null) frames=replay.Frames;
        else
        {
            if(!options.TryGetValue("--card-size",out var cardSize)) throw new ArgumentException("--card-size W,H required for declared manual geometry; otherwise provide --replay");
            var size=ParsePair(cardSize);
            frames=Enumerable.Range(0,17).Select(i=>new EffectFrame(i*16,position.X,position.Y,size.X,size.Y)).ToList();
        }
        int selected=options.TryGetValue("--counter",out var counterText)?int.Parse(counterText,CultureInfo.InvariantCulture):frames[0].Counter;
        int index=frames.FindIndex(f=>f.Counter==selected);
        if(index<0) throw new ArgumentException("Counter is not a captured/manual frame");
        byte[] background=options.TryGetValue("--background",out var backgroundPath)?LoadPixels(backgroundPath):Effect.Checkerboard();
        byte[]? reference=options.TryGetValue("--reference",out var referencePath)?LoadPixels(referencePath):null;
        int? referenceCounter=options.TryGetValue("--reference-counter",out var referenceText)?int.Parse(referenceText,CultureInfo.InvariantCulture):null;
        // Validate every captured quad against reconstruction before accepting measured playback.
        if(replay!=null)
            for(int i=0;i<frames.Count;i++)
            {
                var f=frames[i];int scale=table[(f.Counter-128)&511];
                Point[][] expected=scale==0?[]:[Effect.Quad(164,164,f.X,f.Y,f.Counter/2,scale,table),Effect.Quad(164,164,f.X,f.Y,f.Counter,scale,table)];
                if(expected.Length!=replay.CapturedQuads[i].Length || expected.Where((q,j)=>!q.SequenceEqual(replay.CapturedQuads[i][j])).Any())
                    throw new InvalidDataException($"Captured geometry mismatch at frame {i}; playback refused");
            }
        if(options.ContainsKey("--check"))
        {
            Checks.Run();
            foreach(var f in frames) _=Effect.Render(f,lower,upper,background,table);
            Console.WriteLine($"Loaded two original YGAs; rendered {frames.Count} bounded frames. Visual parity remains unverified.");return 0;
        }
        byte[] pixels=Effect.Render(frames[index],lower,upper,background,table);
        if(options.TryGetValue("--export-dir",out var directory))
        {
            directory=Png.ExportDirectory(directory);
            string stem=$"activation-{frames[index].Counter:000}";
            Png.Write(Path.Combine(directory,stem+".png"),800,600,pixels);
            string? comparison=null;
            if(reference!=null && referenceCounter==frames[index].Counter)
            {
                var (heat,summary)=Difference(pixels,reference,frames[index].X,frames[index].Y);
                Png.Write(Path.Combine(directory,stem+"-difference.png"),800,600,heat);comparison=summary;
                Console.WriteLine(summary);
            }
            var metadata=new { referenceCounterDeclared=referenceCounter,backgroundProvided=options.ContainsKey("--background"),comparison,schema="activation-preview-v1", evidence=replay==null?"static_reconstruction_declared_geometry":"captured_timeline_reconstructed_pixels",
                windowsPixelParityVerified=false,frame=frames[index],pixelSha256=Convert.ToHexString(SHA256.HashData(pixels)).ToLowerInvariant() };
            using(var json=new StreamWriter(new FileStream(Path.Combine(directory,stem+".json"),FileMode.CreateNew)))
                json.Write(JsonSerializer.Serialize(metadata,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(Path.Combine(directory,stem+".png"));
        }
        if(options.ContainsKey("--headless")) return 0;
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(1600,710,"Joey activation research - visual parity UNVERIFIED");
        try
        {
            Raylib.SetTargetFPS(60); // UI refresh only; never used as the original effect clock.
            var outputTexture=Texture(pixels);
            var referenceTexture=Texture(reference??background);
            var diffTexture=Texture(background);
            try
            {
                bool playing=false, difference=false;var clock=new Stopwatch();double playbackStart=0;
                int uploaded=-1;string metric="No paired reference";
                while(!Raylib.WindowShouldClose())
                {
                    if(Raylib.IsKeyPressed(KeyboardKey.Right)) {index=Math.Min(index+1,frames.Count-1);playing=false;}
                    if(Raylib.IsKeyPressed(KeyboardKey.Left)) {index=Math.Max(index-1,0);playing=false;}
                    if(Raylib.IsKeyPressed(KeyboardKey.R)) {index=0;playing=false;}
                    if(Raylib.IsKeyPressed(KeyboardKey.D)) difference=!difference;
                    if(Raylib.IsKeyPressed(KeyboardKey.Space) && replay!=null)
                    {playing=!playing;if(playing){playbackStart=frames[index].Seconds!.Value;clock.Restart();}}
                    if(playing)
                    {
                        double now=playbackStart+clock.Elapsed.TotalSeconds;
                        while(index+1<frames.Count && frames[index+1].Seconds<=now) index++;
                        if(index==frames.Count-1) playing=false;
                    }
                    if(uploaded!=index)
                    {
                        pixels=Effect.Render(frames[index],lower,upper,background,table);
                        Raylib.UpdateTexture(outputTexture,pixels);
                        if(reference!=null && referenceCounter==frames[index].Counter)
                        {
                            var (diff,summary)=Difference(pixels,reference,frames[index].X,frames[index].Y);
                            Raylib.UpdateTexture(diffTexture,diff);metric=summary;
                        }
                        else metric=reference==null?"No paired reference":"Reference counter differs; metric withheld";
                        uploaded=index;
                    }
                    Raylib.BeginDrawing();Raylib.ClearBackground(new RColor(22,24,29,255));
                    Raylib.DrawText("RECONSTRUCTION - BLUE SPELL/TRAP ACTIVATION",12,10,18,RColor.White);
                    Raylib.DrawText(replay==null?"Manual counter; geometry declared; native timing UNKNOWN":"Captured QPC timeline; reconstructed raster; pixel parity UNVERIFIED",12,34,16,RColor.Yellow);
                    Raylib.DrawTexture(outputTexture,0,60,RColor.White);
                    Raylib.DrawTexture(difference && reference!=null && referenceCounter==frames[index].Counter?diffTexture:referenceTexture,800,60,RColor.White);
                    Raylib.DrawText(difference?"RIGHT: ABS RGB DIFFERENCE x4":"RIGHT: ORIGINAL REFERENCE (or chosen background)",810,12,16,RColor.White);
                    Raylib.DrawText($"counter={frames[index].Counter} frame={index+1}/{frames.Count} | arrows: step | Space: captured playback | D: diff | R: restart",12,669,16,RColor.White);
                    Raylib.DrawText(metric,12,691,14,RColor.LightGray);
                    Raylib.EndDrawing();
                }
            }
            finally {Raylib.UnloadTexture(outputTexture);Raylib.UnloadTexture(referenceTexture);Raylib.UnloadTexture(diffTexture);}
        }
        finally {Raylib.CloseWindow();}
        return 0;
    }
    catch(Exception error) {Console.Error.WriteLine(error.Message);return 1;}
}
static Point ParsePair(string value)
{
    string[] fields=value.Split(',');if(fields.Length!=2)throw new ArgumentException("Expected pair X,Y");
    return new(int.Parse(fields[0],CultureInfo.InvariantCulture),int.Parse(fields[1],CultureInfo.InvariantCulture));
}
static unsafe byte[] LoadPixels(string path)
{
    var image=Raylib.LoadImage(path);
    try
    {
        if(image.Width!=800 || image.Height!=600) throw new InvalidDataException("Background/reference must be an unscaled 800x600 client image");
        Raylib.ImageFormat(ref image,PixelFormat.UncompressedR8G8B8A8);
        return new ReadOnlySpan<byte>(image.Data,800*600*4).ToArray();
    }
    finally {Raylib.UnloadImage(image);}
}
static unsafe Texture2D Texture(byte[] data)
{
    fixed(byte* p=data)
    {
        var image=new Image{Data=p,Width=800,Height=600,Mipmaps=1,Format=PixelFormat.UncompressedR8G8B8A8};
        var texture=Raylib.LoadTextureFromImage(image);if(texture.Id==0)throw new InvalidDataException("Texture upload failed");
        Raylib.SetTextureFilter(texture,TextureFilter.Point);return texture;
    }
}
static (byte[],string) Difference(byte[] candidate,byte[] reference,int cx,int cy)
{
    var heat=new byte[candidate.Length];long total=0;int maximum=0,changed=0,count=0;
    for(int y=0;y<600;y++)for(int x=0;x<800;x++)
    {
        int at=(y*800+x)*4;bool roi=Math.Abs(x-cx)<128 && Math.Abs(y-cy)<128;bool different=false;
        for(int c=0;c<3;c++)
        {
            int delta=Math.Abs(candidate[at+c]-reference[at+c]);heat[at+c]=(byte)Math.Min(255,delta*4);
            if(roi) {total+=delta;count++;maximum=Math.Max(maximum,delta);different|=delta!=0;}
        }
        heat[at+3]=255;if(roi && different)changed++;
    }
    return (heat,$"ROI around effect: mean absolute RGB={total/(double)count:F4}/255; max={maximum}; differing pixels={changed}. Alignment/background must match.");
}
