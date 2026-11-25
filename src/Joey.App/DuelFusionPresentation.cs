using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    static string[] liveFusionResources = [];

    static void ConfigureLiveGeneralFusion(Archive archive)
    {
        try
        {
            liveFusionResources = DatGraphics.ReadGeneralFusionBank(archive);
        }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException or IOException)
        {
            liveFusionResources = [];
            Console.Error.WriteLine("[fusion] General fusion visuals unavailable: " + ex.Message);
        }
    }

    static bool TryQueueGeneralFusion(DemoSpecialSummon summon)
    {
        if (!presentation.Style.PlayAnimations || liveFusionResources.Length == 0 ||
            summon.FusionMaterials is not { Count: 2 or 3 } materials) return false;
        try
        {
            var minis = materials.Append(summon.Card).Select(card => graphics.Artwork.MiniFor(card) ??
                throw new InvalidDataException("Missing fusion miniature: " + card.Id)).ToArray();
            foreach (string mini in minis)
            {
                var texture = graphics.Texture(mini);
                if (texture.Width != DuelCardLayout.MiniWidth || texture.Height != DuelCardLayout.MiniHeight)
                    throw new InvalidDataException("Unexpected fusion miniature dimensions: " + mini);
            }
            var sizes = liveFusionResources.Select((path, index) =>
            {
                var texture = graphics.Texture(path);
                return NativeFusionResources.ValidateObservedSize(index, texture.Width, texture.Height);
            }).ToArray();
            var playback = new NativeFusionPlayback(materials.Count);
            playback.AdvanceTo(0, effectCosine, key => sizes[key]);
            QueueCardEffect(new GeneralFusionPlacement(summon, playback, minis, sizes), NativeFusionPlayback.Updates);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException or IOException)
        {
            Console.Error.WriteLine("[fusion] Keeping existing special summon visual: " + ex.Message);
            return false;
        }
    }

    static bool FusionResultHidden(int player, int zone) =>
        cardEffects.Pending.Any(step => step.Kind is GeneralFusionPlacement fusion &&
            fusion.Summon.Player == player && fusion.Summon.Zone == zone &&
            ReferenceEquals(duel.Monsters[player, zone], fusion.Summon.Card));

    static void DrawLiveGeneralFusion(GeneralFusionPlacement fusion)
    {
        if (fusion.Playback.Effect is { } frame)
        {
            DrawFusionReviewPass(NativeGeneralFusionEffect.Center, frame.Primary, liveFusionResources);
            if (frame.Additional is { } additional)
                DrawFusionReviewPass(NativeGeneralFusionEffect.Center, additional, liveFusionResources);
            foreach (var particle in frame.Particles)
            {
                var texture = graphics.Texture(liveFusionResources[particle.Key]);
                Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
                    ToRectangle(particle.Destination), Vector2.Zero, 0, Color.White);
            }
        }
        if (fusion.Playback.Cards is not { } cardsFrame) return;
        void DrawMini(int index, CardBounds bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            var texture = graphics.Texture(fusion.Miniatures[index]);
            Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
                ToRectangle(bounds), Vector2.Zero, 0, Color.White);
        }
        foreach (var material in cardsFrame.Materials) DrawMini(material.MaterialIndex, material.Destination);
        if (cardsFrame.Result is { } result) DrawMini(fusion.Miniatures.Length - 1, result);
    }
}
