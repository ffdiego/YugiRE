using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static Card? statusReviewCard;
    static NativeDisabledChainTarget statusReviewTarget;
    static int statusReviewPerspective, statusReviewType, statusReviewAttribute, statusReviewGlobal;
    static ushort statusReviewFlags, statusReviewDescriptor;
    static NativeCardStatusTransition statusReviewTransition, statusReviewInitialTransition;
    static NativeStatusResource[] statusReviewResources = [];
    static NativeCardStatusPass[] statusReviewPasses = [];

    // Synthetic visual-state input, not a mutation of DemoDuel or a claim
    // that these effects belong to the selected card. All artwork is local DAT.
    static void ConfigureCardStatusReview(string[] args, Archive archive)
    {
        if (Option(args, "--review-card-status=") is not string value) return;
        var parts = value.Split(',');
        if (parts.Length != 6 ||
            !ushort.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out ushort id) ||
            !ushort.TryParse(parts[1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out statusReviewDescriptor) ||
            !ushort.TryParse(parts[2], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out statusReviewFlags) ||
            !int.TryParse(parts[3], out statusReviewType) || statusReviewType is < 0 or > 20 ||
            !int.TryParse(parts[4], out statusReviewAttribute) || statusReviewAttribute is < 0 or > 6 ||
            !int.TryParse(parts[5], out int state) || state is not (0 or 3 or 4))
            throw new InvalidDataException("Expected card ID,hex descriptor,hex flags,effective type,effective attribute,state(0/3/4).");
        statusReviewCard = cards.FirstOrDefault(card => card.Id == id) ??
            throw new InvalidDataException("Unknown card resource ID.");
        if (!statusReviewCard.IsMonster || id == 0x48a)
            throw new InvalidDataException("Review supports ordinary monster cards only.");
        try { statusReviewTarget = new NativeDisabledChainTarget(statusReviewDescriptor); }
        catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
        if (statusReviewTarget.Region != 0)
            throw new InvalidDataException("Review currently uses monster slots only.");
        if (Option(args, "--review-perspective=") is string perspective &&
            (!int.TryParse(perspective, out statusReviewPerspective) || statusReviewPerspective is < 0 or > 1))
            throw new InvalidDataException("Review perspective must be 0 or 1.");
        statusReviewInitialTransition = state switch {
            3 => NativeCardStatusTransition.First(), 4 => NativeCardStatusTransition.Second(), _ => default
        };
        statusReviewTransition = statusReviewInitialTransition;
        statusReviewResources = NativeCardStatusResources.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeCardStatusResources.Manifest)),
            archive.Entries.Select(entry => entry.Name));
        if (graphics.Artwork.MiniFor(statusReviewCard) is null)
            throw new InvalidDataException("Selected card has no original miniature.");
    }

    static void ValidateCardStatusReview()
    {
        for (int i = 4; i <= 9; i++)
        {
            var atlas = StatusReviewAtlas(i);
            if (atlas.Width <= 0 || atlas.Height <= 0 || atlas.Width % atlas.Columns != 0)
                throw new InvalidDataException("Invalid status atlas dimensions: " + statusReviewResources[i].Path);
        }
        var font = graphics.Texture(NativeCardStatusDigits.FontResource);
        if (font.Width != NativeCardStatusDigits.EvidenceAtlasWidth ||
            font.Height != NativeCardStatusDigits.EvidenceAtlasHeight)
            throw new InvalidDataException("Unexpected original yellow numeric atlas dimensions.");
    }

    static NativeStatusAtlas StatusReviewAtlas(int index)
    {
        var resource = statusReviewResources[index];
        var texture = graphics.Texture(resource.Path);
        return new(texture.Width, texture.Height, resource.Columns);
    }

    static void DrawCardStatusReview()
    {
        if (statusReviewCard is not { } card) return;
        if (Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            statusReviewGlobal = 0;
            statusReviewTransition = statusReviewInitialTransition;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.F7))
        {
            statusReviewGlobal++;
            statusReviewTransition = statusReviewTransition.Advance(cardSettled: true);
        }
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground), 0, 0, Color.White);
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board, new Rectangle(0, 0, board.Width, board.Height),
                ToRectangle(DuelSurfaceLayout.Board), Vector2.Zero, 0, Color.White);
        bool near = statusReviewTarget.Side == statusReviewPerspective;
        bool faceDown = (statusReviewDescriptor & 0x8000) == 0;
        var center = statusReviewTarget.Center(statusReviewPerspective);
        var mini = graphics.Texture(graphics.Artwork.MiniFor(card)!);
        // Use the actual resource dimensions as the native bank's width/height.
        // The displayed card itself follows the existing board raster contract.
        DrawDuelCard(card, DuelCardLayout.OnBoard(DuelCardZone.Monster, near,
            statusReviewTarget.Slot, statusReviewTarget.Rotated, faceDown));
        var font = graphics.Texture(NativeCardStatusDigits.FontResource);
        var input = new NativeCardStatusInput(statusReviewDescriptor, statusReviewPerspective,
            center, mini.Width, mini.Height, true, true, card.ClassCode,
            statusReviewType, (int)(card.Properties >> 29), statusReviewAttribute,
            statusReviewFlags, statusReviewTransition, statusReviewGlobal);
        statusReviewPasses = NativeCardStatusComposition.Compose(input,
            StatusReviewAtlas, font.Width, font.Height);
        foreach (var pass in statusReviewPasses)
        {
            string name = pass.IsDigit ? NativeCardStatusDigits.FontResource :
                statusReviewResources[pass.ResourceIndex].Path;
            Raylib.DrawTextureRec(graphics.Texture(name), ToRectangle(pass.Source),
                new Vector2(pass.Destination.X, pass.Destination.Y), Color.White);
        }
    }

    static void CaptureCardStatusReview(RenderTexture2D canvas)
    {
        string basename = SaveVisualReviewImage(canvas, "card-status");
        File.WriteAllText(basename + ".json", JsonSerializer.Serialize(new {
            schemaVersion = 1, source = "YugiRE V08 isolated review; synthetic state",
            descriptor = statusReviewDescriptor, perspective = statusReviewPerspective,
            cardResourceId = statusReviewCard?.Id, flags = statusReviewFlags,
            effectiveType = statusReviewType, effectiveAttribute = statusReviewAttribute,
            globalCounter = statusReviewGlobal, transition = statusReviewTransition,
            center = statusReviewTarget.Center(statusReviewPerspective),
            passes = statusReviewPasses.Select(pass => new {
                pass.IsDigit, pass.ResourceIndex, pass.Source, pass.Destination,
                resource = pass.IsDigit ? NativeCardStatusDigits.FontResource :
                    statusReviewResources[pass.ResourceIndex].Path }),
            compositor = "Existing raylib decoder/blend; native comparison pending"
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        Console.WriteLine("[visual-review] " + Path.GetFullPath(basename + ".json"));
    }
}
