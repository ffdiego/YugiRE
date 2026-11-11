namespace Joey.Core;

/// <summary>Contributions only. Equipment activation, suppression and link eligibility are separate rules.</summary>
public readonly record struct NativeAiAttributeDelta(int Attack, int Defense);

/// <summary>A closed manifest of modifiers whose eligibility has already been resolved.
/// Unknown is the default. Resolved() is an explicit caller assertion that no omitted
/// intrinsic, field, link or global stat modifier applies to the supported plain monster.</summary>
public sealed class NativeAiAttributeContext
{
    public bool IsResolved { get; }
    public IReadOnlyList<ushort> EquipmentResources { get; }
    public int ShieldSwordCount { get; }
    public int MirrorWallCount { get; }
    public bool ReverseDeltaSign { get; }

    private NativeAiAttributeContext(bool resolved, IEnumerable<ushort> equipment,
        int shieldSwordCount, int mirrorWallCount, bool reverseDeltaSign)
    {
        IsResolved = resolved;
        EquipmentResources = Array.AsReadOnly(equipment.ToArray());
        ShieldSwordCount = shieldSwordCount;
        MirrorWallCount = mirrorWallCount;
        ReverseDeltaSign = reverseDeltaSign;
    }

    public static NativeAiAttributeContext Unknown { get; } = new(false, [], 0, 0, false);

    public static NativeAiAttributeContext Resolved(IEnumerable<ushort>? eligibleEquipment = null,
        int shieldSwordCount = 0, int mirrorWallCount = 0, bool reverseDeltaSign = false)
    {
        if (shieldSwordCount < 0) throw new ArgumentOutOfRangeException(nameof(shieldSwordCount));
        if (mirrorWallCount < 0) throw new ArgumentOutOfRangeException(nameof(mirrorWallCount));
        return new(true, eligibleEquipment ?? [], shieldSwordCount, mirrorWallCount, reverseDeltaSign);
    }
}

/// <summary>Verified attribute primitives from 0056FD40, 005674E0, 005BE050,
/// 005ADDC0 and the tail of 0056DD30. This is a bounded stat evaluator,
/// not a replacement for native effect/link discovery.</summary>
public static class NativeAiAttributes
{
    private static readonly Dictionary<ushort, NativeAiAttributeDelta> fixedDeltas = new()
    {
        [0x28a] = new(500,0), [0x291] = new(300,300), [0x12f] = new(1000,0),
        [0x136] = new(500,0), [0x138] = new(0,800), [0x139] = new(700,700),
        [0x140] = new(700,0), [0x412] = new(500,500), [0x422] = new(-500,-500),
        [0x4da] = new(-500,0), [0x58c] = new(1000,-1000)
    };
    private static readonly Dictionary<ushort, byte> type300Both = new()
    {
        [300] = 15, [0x12e] = 3, [0x130] = 10, [0x133] = 11, [0x135] = 13,
        [0x137] = 17, [0x13a] = 1, [0x13b] = 19, [0x13e] = 12,
        [0x141] = 2, [0x142] = 18, [0x144] = 7, [0x145] = 9,
        [0x146] = 16, [0x147] = 14
    };
    private static readonly Dictionary<ushort, byte> attribute400Minus200 = new()
    {
        [0x12d] = 2, [0x132] = 1, [0x134] = 3,
        [0x143] = 5, [0x3f4] = 4, [0x3f5] = 6
    };
    private static readonly ushort[][] aliases =
    [
        [0x3eb,0x40a], [0x3d,0x4e1], [0x22,0x4ba], [0x39,0x76d], [0x4dd,0x76e]
    ];

    // IDs map through 004022E0 BEFORE 005674E0. No normalization of internal indices.
    public static ushort NormalizeResource(ushort resourceId) =>
        (ushort)(resourceId >= 2000 ? resourceId - 2000 : resourceId);

    public static bool EquivalentResources(ushort left, ushort right)
    {
        left = NormalizeResource(left); right = NormalizeResource(right);
        return left == right || aliases.Any(pair => pair.Contains(left) && pair.Contains(right));
    }

    /// <summary>Raw 005BE050 integer expression, including 32-bit wrap and signed division.
    /// It equals ceil(value/2) only for nonnegative values without intermediate overflow.</summary>
    public static int HalfNative(int value) => NativeAiCombat.NativeHalf(value);

    /// <summary>005ADDC0 hidden-opponent branch. Metadata is zero, not the hidden identity.
    /// This is an AI estimate and must never be used as actual battle statistics.</summary>
    public static NativeAiStats HiddenOpponent(int level)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        int value = level > 4 ? 1900 : 1000;
        return new(0,0,0,value,value);
    }

    /// <summary>Subset of 0056FD40. AttributeCode is the byte stored at stats+3;
    /// historical Python accepts that same value shifted left by eight.</summary>
    public static NativeAiResult<NativeAiAttributeDelta> EquipmentDelta(ushort resourceId,
        NativeAiStats context, ushort monsterResourceId)
    {
        if (fixedDeltas.TryGetValue(resourceId, out var delta))
            return NativeAiResult<NativeAiAttributeDelta>.Supported(delta);
        if (type300Both.TryGetValue(resourceId, out byte type))
            return Known(context.TypeCode == type ? new(300,300) : default);
        if (attribute400Minus200.TryGetValue(resourceId, out byte attribute))
            return Known(context.AttributeCode == attribute ? new(400,-200) : default);
        return resourceId switch
        {
            0x131 => Known(context.TypeCode == 10 ? new(700,0) : default),
            0x28d => Known(context.AttributeCode == 4 ? new(700,0) : default),
            0x29b => Known(context.AttributeCode == 1 ? new(700,0) : default),
            0x13c => Known(monsterResourceId is 0x3d or 0x3e or 0x4e1 ? new(500,0) : default),
            0x49e => Known(context.TypeCode == 15 ? new(700,0) : default),
            0x58e => Known(context.TypeCode == 15 ? new(800,0) : default),
            0x604 => Known(monsterResourceId == 0x53b ? new(300,0) : default),
            _ => NativeAiResult<NativeAiAttributeDelta>.Unsupported($"Equipment contribution 0x{resourceId:X4} is not implemented")
        };
    }

    private static NativeAiResult<NativeAiAttributeDelta> Known(NativeAiAttributeDelta value) =>
        NativeAiResult<NativeAiAttributeDelta>.Supported(value);

    /// <summary>0056DD30 final arithmetic AFTER intrinsic/link discovery has filled the
    /// working stats and additive deltas: Shield/Sword swap, signed delta, clamp,
    /// then one native half per Mirror Wall count. No second clamp follows halving.</summary>
    public static NativeAiStats ApplyResolvedTail(NativeAiStats workingStats,
        NativeAiAttributeDelta deltas, int shieldSwordCount, int mirrorWallCount,
        bool reverseDeltaSign = false)
    {
        if (shieldSwordCount < 0) throw new ArgumentOutOfRangeException(nameof(shieldSwordCount));
        if (mirrorWallCount < 0) throw new ArgumentOutOfRangeException(nameof(mirrorWallCount));
        int attack = workingStats.Attack, defense = workingStats.Defense;
        if ((shieldSwordCount & 1) != 0) (attack,defense) = (defense,attack);
        attack = reverseDeltaSign ? unchecked(attack - deltas.Attack) : unchecked(attack + deltas.Attack);
        defense = reverseDeltaSign ? unchecked(defense - deltas.Defense) : unchecked(defense + deltas.Defense);
        attack = Math.Max(0,attack); defense = Math.Max(0,defense);
        for (int i=0;i<mirrorWallCount;i++) attack = HalfNative(attack);
        return workingStats with {Attack=attack,Defense=defense};
    }

    /// <summary>Small complete plain-monster subset: Axe Raider and Gemini Elf.
    /// The caller supplies a resolved manifest; no native metrics or score are needed.
    /// Other monster resources are explicitly unsupported, even when they look vanilla.</summary>
    public static NativeAiResult<NativeAiStats> FromResolvedContext(ushort monsterResourceId,
        NativeAiStats baseStats, NativeAiAttributeContext? context = null)
    {
        if (context is null || !context.IsResolved)
            return NativeAiResult<NativeAiStats>.Unsupported("Attribute/effect context has not been resolved");
        if (monsterResourceId is not (0x004d or 0x0213))
            return NativeAiResult<NativeAiStats>.Unsupported($"Intrinsic stat policy for resource 0x{monsterResourceId:X4} is not implemented");
        if (baseStats.CardIndex == 0 || baseStats.CardIndex > 0xfff)
            return NativeAiResult<NativeAiStats>.Unsupported("Expected an occupied internal monster index in 1..4095");
        var sum = new NativeAiAttributeDelta();
        foreach (ushort equipment in context.EquipmentResources)
        {
            var contribution = EquipmentDelta(equipment,baseStats,monsterResourceId);
            if (!contribution.IsSupported)
                return NativeAiResult<NativeAiStats>.Unsupported(contribution.UnsupportedReasons.ToArray());
            var value = contribution.Value;
            sum = new(unchecked(sum.Attack + value.Attack), unchecked(sum.Defense + value.Defense));
        }
        return NativeAiResult<NativeAiStats>.Supported(ApplyResolvedTail(baseStats,sum,
            context.ShieldSwordCount,context.MirrorWallCount,context.ReverseDeltaSign));
    }

    /// <summary>00402650 base monster attribute is propertyWord >>29 (type21/22
    /// exceptions are excluded by this plain-monster subset). No native stat input.</summary>
    public static NativeAiResult<NativeAiStats> FromPlainCard(Card card,
        NativeAiAttributeContext? context = null) =>
        FromPlainCard(card,(byte)(card.Properties >> 29),context);

    /// <summary>Catalog-backed plain subset. Type comes from the static property word;
    /// baseAttributeCode is explicit because field-driven type/attribute remapping is
    /// not implemented. Resolved context asserts those remappings do not apply.</summary>
    public static NativeAiResult<NativeAiStats> FromPlainCard(Card card, byte baseAttributeCode,
        NativeAiAttributeContext? context = null)
    {
        if (!card.IsMonster || card.Index is <= 0 or > 0xfff)
            return NativeAiResult<NativeAiStats>.Unsupported("Expected a catalog monster with an occupied internal index");
        return FromResolvedContext(card.Id,new((ushort)card.Index,(byte)card.ClassCode,
            baseAttributeCode,card.Attack,card.Defense),context);
    }
}
