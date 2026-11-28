using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Text.Json;

static partial class Game
{
    static NativeResultBank? resultBustReview;
    static NativeResultPlayback? resultBustPlayback;
    static int resultBustVariant;
    static bool resultBustWaitAudio,resultBustAudioComplete,resultBustVoiceStarted;
    static uint? resultBustSound;
    static void ConfigureResultBustReview(string value,string[] args,Archive archive)
    {
        var parts=value.Split(':');
        int code=parts[0] switch {"win"=>1,"lose"=>2,"draw"=>3,_=>0};
        if(code==0 || parts.Length!=2 || !int.TryParse(parts[1],out resultBustVariant) ||
            resultBustVariant<0 || resultBustVariant>=(code==3?1:3))
            throw new InvalidDataException("Expected result:win:0..2, result:lose:0..2 or result:draw:0.");
        resultBustReview=ReadResultBustBank(archive,code);
        resultBustWaitAudio=args.Contains("--reaction-wait-audio");
        ResetResultBustReview();
    }

    static void ResetResultBustReview()
    {
        duelAudio?.StopOpeningVoice();
        resultBustVoiceStarted=false;
        resultBustPlayback=new(resultBustReview!.NativeResult,resultBustVariant);
        resultBustAudioComplete=!resultBustWaitAudio;
        resultBustSound=null;
    }
    static void InitializeResultBustReview() => ValidateResultBustTextures(resultBustReview!);
    static NativeResultLayer[] ResultBustLayers()=>resultBustPlayback!.Active
        ?resultBustReview!.Sample(resultBustPlayback.Counter,resultBustVariant,effectCosine):[];
    static void DrawResultBustReview()
    {
        var playback=resultBustPlayback!;
        if(resultBustWaitAudio && Raylib.IsKeyPressed(KeyboardKey.F9) && playback.VoiceRequested)
            resultBustAudioComplete=true;
        bool advance=ReviewNativeStep(playback.Active,ResetResultBustReview);
        bool paused=nativeReviewClock.FramesPerSecond==0;
        duelAudio?.SetOpeningVoicePaused(paused);
        if(advance)
        {
            playback=resultBustPlayback!;
            if(!resultBustWaitAudio && resultBustVoiceStarted)
                resultBustAudioComplete=duelAudio?.OpeningVoiceComplete!=false;
            if(playback.AdvanceNativeUpdate(resultBustAudioComplete) is uint request)
            {
                resultBustSound=request;
                if(!resultBustWaitAudio && presentation.Style.PlayJoeyVoice)
                    resultBustVoiceStarted=duelAudio?.PlayResultVoice(request)==true;
                if(resultBustVoiceStarted)
                {
                    resultBustAudioComplete=false;
                    duelAudio?.SetOpeningVoicePaused(paused);
                }
            }
            playback.CompleteNativeDraw();
            if(!playback.Active)
            {
                nativeReviewClock.Pause();
                duelAudio?.StopOpeningVoice();
            }
        }
        DrawResultBustLayers(ResultBustLayers());
    }
    static void CaptureResultBustReview(RenderTexture2D canvas)
    {
        var p=resultBustPlayback!;
        string basename=SaveVisualReviewImage(canvas,"result-bust");
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new
        {
            schemaVersion=1,routine="00416260",manifest=resultBustReview!.Manifest,
            nativeResult=resultBustReview.NativeResult,variant=resultBustVariant,
            counter=p.Counter,active=p.Active,voiceRequested=p.VoiceRequested,
            lastSoundRequest=resultBustSound,audioPredicate=resultBustAudioComplete,
            simulatedAudio=resultBustWaitAudio || !resultBustVoiceStarted,selectorKnown=true,
            faceTable=new[]{-1,0,1,0},voiceStarted=resultBustVoiceStarted,
            voiceResource=NativeResultPlayback.VoicePath(p.VoiceRequest),playbackFps=nativeReviewClock.FramesPerSecond,
            layers=ResultBustLayers(),compositor="diagnostic layered preview; shadow/blend not verified"
        },new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    }
}
