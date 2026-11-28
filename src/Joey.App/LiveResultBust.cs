using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Text;

static partial class Game
{
    static readonly Dictionary<int, NativeResultBank> liveResultBanks = new();
    static readonly PresentationTickClock liveResultClock = new();
    static NativeResultBank? liveResultBank;
    static NativeResultPlayback? liveResultPlayback;
    static int liveResultVariant;
    static bool liveResultVoiceStarted;

    static NativeResultBank ReadResultBustBank(Archive archive, int code)
    {
        string manifest = NativeJoeyCinematicRouting.Result(code).Manifest;
        return NativeResultBank.Parse(code, Encoding.Latin1.GetString(archive.Read(manifest)),
            archive.Entries.Select(entry => entry.Name));
    }

    static void ConfigureLiveResultBust(Archive archive)
    {
        liveResultBanks.Clear();
        foreach (int code in new[] { 1, 2, 3 })
        {
            try
            {
                liveResultBanks.Add(code, ReadResultBustBank(archive, code));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
            {
                Console.Error.WriteLine($"[result-bust] Skipping unsupported DAT bank {code}: {ex.Message}");
            }
        }
    }

    static void BeginLiveResultBust(int code)
    {
        if (!presentation.Style.PlayAnimations || !liveResultBanks.TryGetValue(code, out var bank))
        {
            EndLiveResultBust();
            return;
        }

        try
        {
            ValidateResultBustTextures(bank);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            Console.Error.WriteLine($"[result-bust] Skipping unsupported presentation: {ex.Message}");
            EndLiveResultBust();
            return;
        }

        liveResultBank = bank;
        liveResultVariant = (int)NativeJoeyCinematicRouting.ResultVariant(
            (uint)bank.VariantCount, (uint)Random.Shared.Next());
        liveResultPlayback = new(code, liveResultVariant);
        liveResultClock.Reset();
        screen = "result-bust";
    }

    static void ResetLiveResultBust()
    {
        if (liveResultPlayback is not null) duelAudio?.StopOpeningVoice();
        liveResultPlayback = null;
        liveResultBank = null;
        liveResultVoiceStarted = false;
        liveResultClock.Reset();
    }

    static void EndLiveResultBust()
    {
        ResetLiveResultBust();
        screen = "menu";
    }

    static void DrawLiveResultBust()
    {
        var playback = liveResultPlayback!;
        bool advance = liveResultClock.Advance(TimeSpan.FromSeconds(Raylib.GetFrameTime()), presentation.Style) > 0;
        if (advance)
        {
            bool audioComplete = !liveResultVoiceStarted || duelAudio?.OpeningVoiceComplete != false;
            if (playback.AdvanceNativeUpdate(audioComplete) is uint request && presentation.Style.PlayJoeyVoice)
                liveResultVoiceStarted = duelAudio?.PlayResultVoice(request) == true;
        }

        DrawResultBustLayers(liveResultBank!.Sample(playback.Counter, liveResultVariant, effectCosine));
        if (!advance) return;
        playback.CompleteNativeDraw();
        if (!playback.Active) EndLiveResultBust();
    }

    static void ValidateResultBustTextures(NativeResultBank bank)
    {
        foreach (var resource in bank.Resources)
        {
            var texture = ResultBustTexture(resource.Path, resource.CornerKey);
            if (texture.Width != resource.Width || texture.Height != resource.Height)
                throw new InvalidDataException("Unexpected result texture dimensions: " + resource.Path);
        }
    }

    static void DrawResultBustLayers(IEnumerable<NativeResultLayer> layers)
    {
        foreach (var layer in layers)
            Raylib.DrawTexture(ResultBustTexture(layer.Path, layer.CornerKey),
                layer.Position.X, layer.Position.Y, Color.White);
    }

    static Texture2D ResultBustTexture(string path, bool cornerKey) =>
        cornerKey ? graphics.CornerKeyedBitmapTexture(path) : graphics.Texture(path);
}
