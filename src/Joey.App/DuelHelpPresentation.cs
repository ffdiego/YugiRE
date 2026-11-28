using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;

static partial class Game
{
    static string? liveHelpResource;
    static NativeHelpTimeline liveHelp = new();

    static void ConfigureLiveHelp(Archive archive)
    {
        liveHelpResource = null;
        try
        {
            liveHelpResource = NativeHelpResources.ResolveInvalidTarget(
                Encoding.Latin1.GetString(archive.Read(NativeHelpResources.Manifest)),
                archive.Entries.Select(entry => entry.Name));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException)
        {
            Console.Error.WriteLine("[help] " + ex.Message);
        }
    }

    static void ResetLiveHelp() => liveHelp = new();

    // Only an occupied field card rejected by an existing target selector
    // may request key 9. This adapter never asks Core to resolve an effect
    // or computes legality from hidden card identity.
    static (NativeHelpTarget Target, CardPoint Center)? InvalidHelpFocus()
    {
        bool monsterChoice = fissureSelection is not null || queuedEquipChoice is not null;
        bool spellChoice = queuedSpellTrapSelection is not null;
        if (!monsterChoice && !spellChoice) return null;

        for (int side = 0; side < 2; side++)
        {
            bool near = side == ViewPlayer;
            if (Hit(FieldZoneBounds(side)) &&
                duel.TryCaptureCardTarget(side, DuelZone.FieldSpell, 0, out _))
            {
                if (spellChoice && IsQueuedSpellTrapTarget(side, SpellTrapTargetRules.FieldZone))
                    return null;
                return (new NativeHelpTarget((ushort)((10 << 1) | side)),
                    DuelCardLayout.Center(DuelCardZone.Field, near, 0));
            }

            for (int slot = 0; slot < 5; slot++)
            {
                if (Hit(ZoneBounds(side, slot, false)) &&
                    duel.TryCaptureCardTarget(side, DuelZone.Monster, slot, out _))
                {
                    if (IsFissureTarget(side, slot) || IsQueuedEquipTarget(side, slot))
                        return null;
                    return (new NativeHelpTarget((ushort)((slot << 1) | side)),
                        DuelCardLayout.Center(DuelCardZone.Monster, near, slot));
                }

                if (Hit(ZoneBounds(side, slot, true)) &&
                    duel.TryCaptureCardTarget(side, DuelZone.SpellTrap, slot, out _))
                {
                    if (spellChoice && IsQueuedSpellTrapTarget(side, slot))
                        return null;
                    return (new NativeHelpTarget((ushort)(((slot + 5) << 1) | side)),
                        DuelCardLayout.Center(DuelCardZone.Spell, near, slot));
                }
            }
        }
        return null;
    }

    static void DrawLiveHelp(int updates, bool allowSelection)
    {
        if (liveHelpResource is not { } resource) return;
        var focus = allowSelection ? InvalidHelpFocus() : null;
        int key = focus.HasValue ? NativeHelpResources.InvalidTargetKey : -1;
        // The App selector supplies the equivalent selection/focus facts.
        // Native virtual +2C / group 6 parity has not been established.
        if (!presentation.Style.PlayAnimations) updates = 6;
        for (int i = 0; i < updates; i++)
            liveHelp.AdvanceUpdate(key, focus?.Target ?? default, ViewPlayer,
                focus.HasValue, false, false, focus.HasValue);

        // Never display an old message on a different target, even on
        // host frames that have not consumed a native-style update.
        if (focus is not { } current || !liveHelp.Visible ||
            liveHelp.Target.Identity != current.Target.Identity) return;

        try
        {
            var texture = graphics.Texture(resource);
            if (texture.Width != NativeHelpResources.InvalidTargetWidth ||
                texture.Height != NativeHelpResources.InvalidTargetHeight)
                throw new InvalidDataException("Unexpected target-rejection help dimensions.");
            int miniHeight = graphics.Texture(DatGraphics.MiniCardBack).Height;
            if (liveHelp.Sample(current.Center.X, current.Center.Y, 0,
                miniHeight, texture.Width, texture.Height, effectCosine) is not { } bounds ||
                bounds.Height <= 0) return;

            // Native +4C geometry, existing YGA texture policy and Raylib
            // point sampling; compositor/pixel parity remains provisional.
            Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
                ToRectangle(bounds), Vector2.Zero, 0, Color.White);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            liveHelpResource = null;
            ResetLiveHelp();
            Console.Error.WriteLine("[help] disabled unsupported bank: " + ex.Message);
        }
    }
}
