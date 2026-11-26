using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace Joey.Core;

internal static class NormalMonsterCatalog
{
    private static readonly FrozenSet<ushort> NormalIds = LoadNormalIds();

    public static bool Contains(ushort resourceId) => NormalIds.Contains(resourceId);

    private static FrozenSet<ushort> LoadNormalIds()
    {
        using var stream = typeof(NormalMonsterCatalog).Assembly.GetManifestResourceStream("Joey.Core.monster-subtypes.json")
            ?? throw new InvalidOperationException("The versioned monster subtype inventory is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("subtypes").EnumerateObject()
            .Where(property => property.Value.ValueEquals("Normal"))
            .Select(property => ushort.Parse(property.Name, CultureInfo.InvariantCulture))
            .ToFrozenSet();
    }
}
