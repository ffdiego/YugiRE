using System.Buffers.Binary;

namespace Joey.Core;

/// <summary>A computed native 0056DD30 buffer. Resource ID is deliberately separate.</summary>
public readonly record struct NativeAiStats(ushort CardIndex, byte TypeCode, byte AttributeCode, int Attack, int Defense)
{
    public static NativeAiStats Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 12) throw new ArgumentException("Expected exactly 12 native stat bytes");
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes),bytes[2],bytes[3],
            BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]),BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]));
    }
    public byte[] ToBytes()
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes,CardIndex); bytes[2]=TypeCode; bytes[3]=AttributeCode;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4),Attack);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8),Defense);
        return bytes;
    }
}

public sealed record NativeAiResult<T>
{
    public bool IsSupported => UnsupportedReasons.Count == 0;
    public T? Value { get; }
    public IReadOnlyList<string> UnsupportedReasons { get; }
    private NativeAiResult(T? value, string[] reasons) { Value=value; UnsupportedReasons=Array.AsReadOnly(reasons); }
    public static NativeAiResult<T> Supported(T value) => new(value,[]);
    public static NativeAiResult<T> Unsupported(params string[] reasons) =>
        new(default,reasons.Length == 0 ? ["Unspecified unsupported state"] : (string[])reasons.Clone());
}

public sealed record NativeAiMonster(NativeAiStats Stats, ushort ResourceId, bool DefensePosition=false,
    bool FaceUp=true, int Level=4, bool? PositionChangeAllowed=null, uint? RuleFlags=null)
{
    // In the closed plain-field subset all other 0056B5A0 effects are absent.
    // Explicit predicate inputs remain available for already-resolved contexts.
    public bool? PositionChangePredicate => PositionChangeAllowed ??
        (RuleFlags is uint flags ? (flags & 0x20000)==0 : null);
    // 005ADDC0: this models the evaluator's knowledge, not real duel statistics.
    public NativeAiStats OpponentStats => FaceUp ? Stats : new(0,0,0,Level <= 4 ? 1000 : 1900,Level <= 4 ? 1000 : 1900);
    public ushort OpponentResourceId => FaceUp ? ResourceId : ushort.MaxValue;
}

/// <summary>Already resolved predicates of 005B0F40. Unknown is the default.
/// ResolvedNoExternalModifiers is an explicit caller assertion, not evidence that
/// an arbitrary real field has no equipment, flags or conditional effects.</summary>
public sealed record NativeAiCombatContext
{
    public bool IsResolved { get; init; }
    public int MetalmorphCount { get; init; }
    public bool PierceToOpponent { get; init; }
    public bool PierceToAttacker { get; init; }
    public bool DefenderSideProtected { get; init; } // opponent playerFlags & 00400000
    public bool AttackerSideProtected { get; init; }
    public bool SuppressDamageToDefender { get; init; } // 058F + 56B220(014D)
    public bool SuppressDamageToAttacker { get; init; }
    public ushort InitialFlags { get; init; }
    public static NativeAiCombatContext ResolvedNoExternalModifiers => new() {IsResolved=true};
}

/// <summary>Exact four-byte event layout; native scratch bits are preserved when supplied.</summary>
public sealed record NativeAiCombatEvent(ushort Flags, short Score)
{
    public int AttackerSlot => (Flags >> 4) & 7;
    public int? TargetSlot => SpecialTarget ? null : (Flags >> 7) & 7;
    public bool SpecialTarget => (Flags & 2) != 0;
    public bool SelectedByOuterLoop => (Flags & 4) != 0;
    public bool Useful => (Flags & 8) != 0;
    public bool AttackerRemoved => (Flags & 0x400) != 0;
    public bool DefenderRemoved => (Flags & 0x800) != 0;
    public bool SelectionReturn => SpecialTarget ? Score > 0 : Useful;
    public static NativeAiCombatEvent Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 4) throw new ArgumentException("Expected exactly four native event bytes");
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes),BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]));
    }
    public byte[] ToBytes()
    {
        byte[] bytes=new byte[4]; BinaryPrimitives.WriteUInt16LittleEndian(bytes,Flags);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2),Score); return bytes;
    }
}

/// <summary>005B0F40 only. It evaluates hypothetical outcomes; it never changes a real duel.</summary>
public static class NativeAiCombat
{
    // 005BE050 uses signed division after int32 wrapping, not floating point/ceil.
    public static int NativeHalf(int value) => unchecked(value*5+5)/10;

    public static NativeAiResult<NativeAiCombatEvent> Evaluate(NativeAiMonster attacker,
        NativeAiMonster defender, int attackerSlot, int defenderSlot, NativeAiCombatContext context)
    {
        if (attackerSlot is < 0 or > 4 || defenderSlot is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(attackerSlot));
        if (!context.IsResolved) return NativeAiResult<NativeAiCombatEvent>.Unsupported("005B0F40 field predicates/effect links are not resolved");
        if (context.MetalmorphCount < 0) throw new ArgumentOutOfRangeException(nameof(context.MetalmorphCount));
        var own=attacker.Stats; var enemy=defender.OpponentStats;
        int attack=own.Attack, defense=own.Defense, enemyAttack=enemy.Attack, enemyDefense=enemy.Defense;
        ushort resource=attacker.ResourceId, enemyResource=defender.OpponentResourceId;
        unchecked
        {
            if (resource==0x1dd && enemy.TypeCode==6) attack+=1000;
            else if (resource==0x4e5 && enemy.TypeCode==15) { attack+=2000; defense+=2000; }
            if (enemyResource==0x11f && own.TypeCode==1) enemyDefense=NativeHalf(enemyDefense);
            else if (enemyResource==0x4e5 && own.TypeCode==15) { enemyAttack+=2000; enemyDefense+=2000; }
            attack+=context.MetalmorphCount*NativeHalf(enemyAttack);
            ushort flags=(ushort)((context.InitialFlags & 0xf005) | attackerSlot<<4 | defenderSlot<<7);
            int gain=0, loss=0, opposing=defender.DefensePosition ? enemyDefense : enemyAttack;
            if (attack==opposing)
            {
                if (!defender.DefensePosition && attack>0) flags|=0xc00;
            }
            else if (attack>opposing)
            {
                int difference=attack-opposing;
                flags|=0x800;
                if (!defender.DefensePosition) gain=difference;
                else
                {
                    if (resource==0x53d || context.PierceToOpponent) gain=difference;
                    if (context.PierceToAttacker) loss=difference;
                }
            }
            else
            {
                loss=opposing-attack;
                if (!defender.DefensePosition) flags|=0x400;
            }
            // Ordering is native: card-specific overrides below can set bits again.
            if (context.DefenderSideProtected) {flags&=0xf7ff;gain=0;}
            if (context.AttackerSideProtected) {flags&=0xfbff;loss=0;}
            if (gain>0 && context.SuppressDamageToDefender) gain=0;
            if (loss>0 && context.SuppressDamageToAttacker) loss=0;
            if (resource==0x199)
            {
                if (enemy.AttributeCode==2) flags|=0x800;
            }
            else if (resource==0x4cf)
            {
                flags&=0xfbff;loss=0;gain=defender.DefensePosition ? 0 : 500;
            }
            else if (resource==0x4e3 && enemyAttack>=1900) flags&=0xfbff;
            short score=(short)(gain-loss);
            bool useful=score>0 || ((flags & 0x800)!=0 && (flags & 0x400)==0);
            flags=(ushort)((flags & 0xfff7) | (useful ? 8 : 0));
            return NativeAiResult<NativeAiCombatEvent>.Supported(new(flags,score));
        }
    }

    /// <summary>005B0D90 special target branch after BF20/BFD0/B05A0 have passed.
    /// The high word is ATK truncated to int16; utility bit is NOT recomputed.
    /// Eligibility for this route must be established separately.</summary>
    public static NativeAiCombatEvent SpecialTarget(int calculatedAttack, int attackerSlot, ushort initialFlags=0)
    {
        if (attackerSlot is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(attackerSlot));
        return new((ushort)(((initialFlags | 2) & 0xf38f) | attackerSlot<<4),unchecked((short)calculatedAttack));
    }
}
