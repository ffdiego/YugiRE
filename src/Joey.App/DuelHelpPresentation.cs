using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;

static partial class Game
{
    static string? liveHelpResource, liveTributeHelpResource;
    static CardTarget? liveTributeHelp;
    static NativeHelpTimeline liveHelp = new();

    static void ConfigureLiveHelp(Archive archive)
    {
        liveHelpResource = null;
        liveTributeHelpResource = null;
        try
        {
            var manifest = Encoding.Latin1.GetString(archive.Read(NativeHelpResources.Manifest));
            var available = archive.Entries.Select(entry => entry.Name).ToArray();
            liveHelpResource = ResolveHelpResource(() => NativeHelpResources.ResolveInvalidTarget(manifest, available));
            liveTributeHelpResource = ResolveHelpResource(() => NativeHelpResources.ResolveTributeRequired(manifest, available));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException)
        {
            Console.Error.WriteLine("[help] " + ex.Message);
        }
    }

    static string? ResolveHelpResource(Func<string> resolve)
    {
        try { return resolve(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException)
        {
            Console.Error.WriteLine("[help] " + ex.Message);
            return null;
        }
    }

    static void ResetLiveHelp()
    {
        liveHelp = new();
        liveTributeHelp = null;
    }

    // Native key 0 was observed on a click, not mere hover (V29.3).
    // Bind the rejected attempt to the occurrence rather than the slot.
    static void RequestTributeHelp(int index)
    {
        if (liveTributeHelpResource is not null && duel.HasInsufficientNormalSummonTributes(index) &&
            duel.TryCaptureCardTarget(ViewPlayer, DuelZone.Hand, index, out var target))
            liveTributeHelp = target;
    }

    static (NativeHelpTarget Target, CardPoint Center)? TributeHelpFocus(bool allowHelp, bool handHovered)
    {
        if (!allowHelp || !handHovered || hoverHand < 0 || liveTributeHelp is not { } requested ||
            !duel.HasInsufficientNormalSummonTributes(hoverHand) ||
            !duel.TryCaptureCardTarget(ViewPlayer, DuelZone.Hand, hoverHand, out var current) ||
            requested != current)
        {
            liveTributeHelp = null;
            return null;
        }
        // Region 11 and slot in bits 6..13; identity excludes high flags.
        var target = new NativeHelpTarget((ushort)((hoverHand << 6) | (11 << 1) | ViewPlayer));
        return (target, DuelCardLayout.FrameCenter(ToCardBounds(HandBounds(hoverHand))));
    }

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

    static void DrawLiveHelp(int updates, bool allowHelp, bool handHovered)
    {
        var tribute = TributeHelpFocus(allowHelp, handHovered);
        var invalid = allowHelp && !handHovered ? InvalidHelpFocus() : null;
        var focus = tribute ?? invalid;
        int key = tribute.HasValue ? NativeHelpResources.TributeRequiredKey :
            invalid.HasValue ? NativeHelpResources.InvalidTargetKey : -1;
        // The App selector supplies the equivalent selection/focus facts.
        // Native virtual +2C / group 6 parity has not been established.
        if (!presentation.Style.PlayAnimations) updates = 6;
        for (int i = 0; i < updates; i++)
            liveHelp.AdvanceUpdate(key, focus?.Target ?? default, ViewPlayer,
                focus.HasValue, false, false, focus.HasValue);

        // Never display an old message on a different target, even on
        // host frames that have not consumed a native-style update.
        if (focus is not { } current || !liveHelp.Visible || liveHelp.MessageKey != key ||
            liveHelp.Target.Identity != current.Target.Identity) return;

        bool isTribute = key == NativeHelpResources.TributeRequiredKey;
        string? resource = isTribute ? liveTributeHelpResource : liveHelpResource;
        if (resource is null) return;
        try
        {
            var texture = graphics.Texture(resource);
            if (texture.Width != (isTribute ? NativeHelpResources.TributeRequiredWidth : NativeHelpResources.InvalidTargetWidth) ||
                texture.Height != (isTribute ? NativeHelpResources.TributeRequiredHeight : NativeHelpResources.InvalidTargetHeight))
                throw new InvalidDataException("Unexpected contextual help dimensions.");
            int miniHeight = graphics.Texture(DatGraphics.MiniCardBack).Height;
            // HandPresentation projects the native 290px large card at half size.
            int handHeight = HandPresentation.CardHeight * 2;
            if (liveHelp.Sample(current.Center.X, current.Center.Y, handHeight,
                miniHeight, texture.Width, texture.Height, effectCosine) is not { } bounds ||
                bounds.Height <= 0) return;

            // Native +4C geometry, existing YGA texture policy and Raylib
            // point sampling; compositor/pixel parity remains provisional.
            Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
                ToRectangle(bounds), Vector2.Zero, 0, Color.White);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            if (isTribute) liveTributeHelpResource = null;
            else liveHelpResource = null;
            ResetLiveHelp();
            Console.Error.WriteLine("[help] disabled unsupported bank: " + ex.Message);
        }
    }
}
