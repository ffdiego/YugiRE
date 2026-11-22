namespace Joey.LookAndFeel;

/// <summary>
/// Recovered from the detail object at 0041BE80 and its text builder
/// 0041B3E0. The original English detail is a GDI rich-text surface,
/// not an 8x8 raylib font or the numeric duel_font* atlases.
/// </summary>
public static class NativeDetailPanel
{
    // Object fields 0x6F..0x72 from 0041BE80:
    // start X, top Y, width and bottom Y of the rich text window.
    public const int TextX = 8;
    public const int TextY = 365;
    public const int TextWidth = 184;
    public const int TextBottom = 526;
    public const int TextHeight = TextBottom - TextY;

    // 0041B3E0 -> 00544B20 sets 14px GDI line height in non-Japanese mode.
    public const int EnglishTextSize = 14;
    public const int EnglishLineAdvance = 14;
    public const string EnglishFace = "Courier New";

    // Original COLORREF values from 0041BE80, not arbitrary client colors.
    public const uint NameColorRef = 0x00101010;
    public const uint StatsColorRef = 0x001E49B4;
    public const uint DescriptionColorRef = 0x000B586E;

    public static (byte R, byte G, byte B) DecodeColorRef(uint colorRef) =>
        ((byte)(colorRef & 0xff),
         (byte)((colorRef >> 8) & 0xff),
         (byte)((colorRef >> 16) & 0xff));

    public enum LineKind { Name, Stats, Description }

    public readonly record struct TextLine(string Value, LineKind Kind);

    /// <summary>
    /// Produce laid-out paragraphs in the sequence emitted by the native
    /// 0041B3E0 builder: title, optional attack/defense, card description.
    /// Actual pixel widths are delegated to the loaded GDI-face font;
    /// splitting a long token avoids overflowing the native 184px viewport.
    /// </summary>
    public static IReadOnlyList<TextLine> Wrap(
        string name, string? stats, string description,
        Func<string, int> measure, int width = TextWidth)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var lines = new List<TextLine>();
        AddParagraphs(name ?? "", LineKind.Name);
        if (!string.IsNullOrWhiteSpace(stats)) AddParagraphs(stats, LineKind.Stats);
        AddParagraphs(description ?? "", LineKind.Description);
        return lines;

        void AddParagraphs(string source, LineKind kind)
        {
            foreach (string paragraph in source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string current = "";
                foreach (string word in paragraph.Split((char[]?)null,
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    string proposed = current.Length == 0 ? word : current + " " + word;
                    if (measure(proposed) <= width)
                    {
                        current = proposed;
                        continue;
                    }

                    if (current.Length != 0)
                    {
                        lines.Add(new(current, kind));
                        current = "";
                    }

                    foreach (char character in word)
                    {
                        string combined = current + character;
                        if (current.Length > 0 && measure(combined) > width)
                        {
                            lines.Add(new(current, kind));
                            current = "";
                        }
                        current += character;
                    }
                }
                // Preserve deliberate line breaks, including empty lines.
                lines.Add(new(current, kind));
            }
        }
    }

    public static int VisibleLineCount(int lineHeight = EnglishLineAdvance)
    {
        if (lineHeight <= 0) throw new ArgumentOutOfRangeException(nameof(lineHeight));
        return TextHeight / lineHeight;
    }

    public static int ClampScroll(int requested, int lineCount, int visibleLines) =>
        Math.Clamp(requested, 0, Math.Max(0, lineCount - visibleLines));
}
