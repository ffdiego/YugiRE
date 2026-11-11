using System.Collections.ObjectModel;

namespace Joey.Core;

/// <summary>Five monster zones per side, owned by this immutable hypothetical field.</summary>
public sealed class NativeAiField
{
    private readonly NativeAiMonster?[][] sides;
    public NativeAiField(IEnumerable<NativeAiMonster?> side0, IEnumerable<NativeAiMonster?> side1)
    {
        sides=[side0.ToArray(),side1.ToArray()];
        if (sides.Any(x=>x.Length!=5)) throw new ArgumentException("Expected five monster zones per side");
    }
    public NativeAiMonster? At(int side,int slot)
    {
        if (side is <0 or >1 || slot is <0 or >4) throw new ArgumentOutOfRangeException(nameof(slot));
        return sides[side][slot];
    }
    public NativeAiField With(int side,int slot,NativeAiMonster? monster)
    {
        _=At(side,slot);
        var copy=sides.Select(x=>(NativeAiMonster?[])x.Clone()).ToArray();copy[side][slot]=monster;
        return new(copy[0],copy[1]);
    }
    public int Count(int side) => Enumerable.Range(0,5).Count(slot=>At(side,slot) is not null);
    public static NativeAiField Empty => new(new NativeAiMonster?[5],new NativeAiMonster?[5]);
}

public readonly record struct NativeAiAttackKey(int Slot,ushort ResourceId,int CalculatedAttack);
public readonly record struct NativeAiAttackPair(int Side,int AttackerSlot,int DefenderSlot);

/// <summary>Closed plain-field contract: all omitted rule/effect gates are asserted absent.
/// Only the two demonstrated vanilla resources are accepted. Stats must remain valid
/// after hypothetical removals (no unmodeled field-dependent modifiers).</summary>
public sealed class NativeAiFieldContext
{
    public bool IsResolved {get;}
    public bool DuelRunning {get;}
    public bool FaceUpPlacementAllowed {get;}
    public IReadOnlyList<ushort> LifePoints {get;}
    public IReadOnlyList<uint> PlayerFlags {get;}
    public IReadOnlyDictionary<NativeAiAttackPair,NativeAiCombatContext> PairContexts {get;}
    private NativeAiFieldContext(bool resolved,ushort lp0,ushort lp1,uint flags0,uint flags1,bool running,
        bool faceUp,IReadOnlyDictionary<NativeAiAttackPair,NativeAiCombatContext>? pairs)
    {
        IsResolved=resolved;DuelRunning=running;FaceUpPlacementAllowed=faceUp;
        LifePoints=Array.AsReadOnly(new[]{lp0,lp1});PlayerFlags=Array.AsReadOnly(new[]{flags0,flags1});
        PairContexts=new ReadOnlyDictionary<NativeAiAttackPair,NativeAiCombatContext>(
            pairs is null ? [] : new Dictionary<NativeAiAttackPair,NativeAiCombatContext>(pairs));
    }
    public static NativeAiFieldContext Unknown {get;}=new(false,0,0,0,0,false,false,null);
    public static NativeAiFieldContext ResolvedPlain(ushort lp0,ushort lp1,uint flags0=0,uint flags1=0,
        bool duelRunning=true,bool faceUpPlacementAllowed=true,
        IReadOnlyDictionary<NativeAiAttackPair,NativeAiCombatContext>? pairContexts=null) =>
        new(true,lp0,lp1,flags0,flags1,duelRunning,faceUpPlacementAllowed,pairContexts);
}

public sealed record NativeAiSequenceEvaluation(int Score,ushort ActionMask,int OwnLosses,int OpponentLosses,
    int OwnSurvivors,int OpponentSurvivors,ushort ScoreSummary,ushort RankingSummary,
    IReadOnlyList<NativeAiCombatEvent> Events,NativeAiField SimulatedField);

/// <summary>Bounded 005B0D90/0B90/0A50 and 005B08B0 port. Rules outside the closed
/// plain subset return Unsupported. This never executes actual duel effects.</summary>
public static class NativeAiSequence
{
    public static uint AttackKey(ushort resource,int attack) =>
        (uint)((resource==0x5f3 ? 1 : resource is 0x2f9 or 0x226 or 0x2e8 ? 3 : 2)<<16) | (unchecked((uint)attack) & 0xffff);

    /// <summary>The native selection sort swaps, so equal keys are not stable.</summary>
    public static IReadOnlyList<int> OrderAttackers(IEnumerable<NativeAiAttackKey> participants)
    {
        var keys=participants.ToArray();
        if (keys.Length>5 || keys.Any(k=>k.Slot is <0 or >4) || keys.Select(k=>k.Slot).Distinct().Count()!=keys.Length)
            throw new ArgumentException("Expected unique monster slots 0..4");
        for(int i=0;i<keys.Length-1;i++)
        {
            uint best=uint.MaxValue;int selected=i;
            for(int j=i;j<keys.Length;j++)
            {
                uint key=AttackKey(keys[j].ResourceId,keys[j].CalculatedAttack);
                if (key<best) {best=key;selected=j;}
            }
            (keys[i],keys[selected])=(keys[selected],keys[i]);
        }
        return Array.AsReadOnly(keys.Select(k=>k.Slot).ToArray());
    }

    // 005B0D90: only visibility of the NEW target is tested.
    public static bool PreferTarget(NativeAiCombatEvent candidate,NativeAiCombatEvent? current,
        bool newTargetFaceUp,int newTargetDefense,int oldTargetDefense) => candidate.Useful &&
        (current is null || !current.Useful || candidate.Score>current.Score ||
         (candidate.Score==current.Score && (!newTargetFaceUp || newTargetDefense>oldTargetDefense)));

    private static string? Validate(NativeAiField field,NativeAiFieldContext context)
    {
        if (!context.IsResolved) return "Field rules and modifier completeness are not resolved";
        for(int side=0;side<2;side++) for(int slot=0;slot<5;slot++)
        {
            var m=field.At(side,slot);if(m is null)continue;
            if (m.Stats.CardIndex is 0 or >0xfff) return "Expected an occupied 12-bit internal monster identity";
            if (m.RuleFlags is null) return $"Native rule flags for side{side}/slot{slot} are unknown";
            if (m.PositionChangeAllowed is bool explicitPosition && explicitPosition!=((m.RuleFlags.Value&0x20000)==0))
                return $"Position-change predicate contradicts closed plain rule flags for side{side}/slot{slot}";
            if(m.ResourceId is not(0x4d or 0x213))
                return $"Field eligibility/stat-removal policy for resource0x{m.ResourceId:X4} is unsupported";
        }
        return null;
    }

    private static NativeAiResult<bool> AttackPolicy(NativeAiField field,int side,int slot,int target,NativeAiFieldContext context)
    {
        var own=field.At(side,slot)!;
        if(target==5 && context.LifePoints[1-side]<=own.Stats.Attack) return NativeAiResult<bool>.Supported(true);
        if(own.PositionChangePredicate is null)
            return NativeAiResult<bool>.Unsupported($"0056B5A0 position-change predicate missing for side{side}/slot{slot}");
        if(!own.PositionChangePredicate.Value) return NativeAiResult<bool>.Supported(true);
        if(target==5 && own.Stats.Attack<300) return NativeAiResult<bool>.Supported(false);
        int maximum=-1;
        for(int t=0;t<5;t++) if(context.LifePoints[1-side]>0 && t!=target && field.At(1-side,t) is {} rival && (rival.RuleFlags!.Value&0x40000)==0)
            maximum=Math.Max(maximum,rival.OpponentStats.Attack);
        return NativeAiResult<bool>.Supported(maximum<unchecked(own.Stats.Attack+1000));
    }

    private static NativeAiResult<NativeAiCombatEvent?> SelectTarget(NativeAiField field,int side,int slot,NativeAiFieldContext context)
    {
        var own=field.At(side,slot)!;
        if(field.Count(1-side)==0)
        {
            var allowed=AttackPolicy(field,side,slot,5,context);
            if(!allowed.IsSupported) return NativeAiResult<NativeAiCombatEvent?>.Unsupported(allowed.UnsupportedReasons.ToArray());
            if(!allowed.Value) return NativeAiResult<NativeAiCombatEvent?>.Supported(null);
            var value=NativeAiCombat.SpecialTarget(own.Stats.Attack,slot,(ushort)side);
            return NativeAiResult<NativeAiCombatEvent?>.Supported(value.SelectionReturn ? value : null);
        }
        NativeAiCombatEvent? best=null;
        for(int target=0;target<5;target++)
        {
            var rival=field.At(1-side,target);if(rival is null)continue;
            // Closed plain-field: BD80 accepts any occupied opponent target.
            var allowed=AttackPolicy(field,side,slot,target,context);
            if(!allowed.IsSupported) return NativeAiResult<NativeAiCombatEvent?>.Unsupported(allowed.UnsupportedReasons.ToArray());
            if(!allowed.Value)continue;
            var pair=new NativeAiAttackPair(side,slot,target);
            var outcome=context.PairContexts.TryGetValue(pair,out var specified) ? specified : NativeAiCombatContext.ResolvedNoExternalModifiers;
            // Player flags are authoritative. The B0B90/B0D90 scratch starts as
            // the side argument (0/1); arbitrary direct-F40 seed bits do not apply here.
            outcome=outcome with {InitialFlags=(ushort)side,
                AttackerSideProtected=(context.PlayerFlags[side]&0x400000)!=0,
                DefenderSideProtected=(context.PlayerFlags[1-side]&0x400000)!=0};
            var evaluated=NativeAiCombat.Evaluate(own,rival,slot,target,outcome);
            if(!evaluated.IsSupported) return NativeAiResult<NativeAiCombatEvent?>.Unsupported(evaluated.UnsupportedReasons.ToArray());
            var candidate=evaluated.Value!;
            int oldDefense=best is null ? 0 : field.At(1-side,best.TargetSlot!.Value)!.Stats.Defense;
            if(PreferTarget(candidate,best,rival.FaceUp,rival.Stats.Defense,oldDefense)) best=candidate;
        }
        return NativeAiResult<NativeAiCombatEvent?>.Supported(best);
    }

    public static NativeAiResult<NativeAiSequenceEvaluation> Evaluate(NativeAiField field,int side,
        NativeAiFieldContext context,ushort initialSummary=0)
    {
        if(side is <0 or >1)throw new ArgumentOutOfRangeException(nameof(side));
        string? error=Validate(field,context);
        if(error is not null)return NativeAiResult<NativeAiSequenceEvaluation>.Unsupported(error);
        var state=field;var events=new List<NativeAiCombatEvent>();ushort mask=0;int score=0,ownLoss=0,opponentLoss=0;
        if(context.DuelRunning && (context.PlayerFlags[side]&0x40000)==0)
        for(int step=0;step<5;step++)
        {
            var participants=new List<NativeAiAttackKey>();
            for(int slot=0;slot<5;slot++)
                if((mask&(1<<slot))==0 && state.At(side,slot) is {FaceUp:true,DefensePosition:false} m && context.LifePoints[side]>0 && (m.RuleFlags!.Value&0x40000)==0)
                    participants.Add(new(slot,m.ResourceId,m.Stats.Attack));
            NativeAiCombatEvent? selected=null;
            foreach(int slot in OrderAttackers(participants))
            {
                var result=SelectTarget(state,side,slot,context);
                if(!result.IsSupported) return NativeAiResult<NativeAiSequenceEvaluation>.Unsupported(result.UnsupportedReasons.ToArray());
                if(result.Value is not null) {selected=result.Value;break;}
            }
            if(selected is null)break;
            selected=selected with {Flags=(ushort)(selected.Flags|4)};events.Add(selected);
            mask|=(ushort)(1<<selected.AttackerSlot);score=unchecked(score+selected.Score);
            if(selected.AttackerRemoved) {state=state.With(side,selected.AttackerSlot,null);ownLoss++;}
            if(selected.DefenderRemoved) {state=state.With(1-side,selected.TargetSlot!.Value,null);opponentLoss++;}
        }
        int ownSurvivors=state.Count(side),opponentSurvivors=state.Count(1-side);
        ushort low=(ushort)((ownLoss&7)|((opponentLoss&7)<<3));
        ushort scoreSummary=(ushort)((initialSummary&0xffc0)|low);
        ushort rankingSummary=(ushort)((scoreSummary&0xf03f)|(ownSurvivors&7)<<6|(opponentSurvivors&7)<<9);
        return NativeAiResult<NativeAiSequenceEvaluation>.Supported(new(score,mask,ownLoss,opponentLoss,
            ownSurvivors,opponentSurvivors,scoreSummary,rankingSummary,Array.AsReadOnly(events.ToArray()),state));
    }

    public static NativeAiResult<NativeAiField> PrepareBaseline(NativeAiField field,int side,NativeAiFieldContext context)
    {
        if(side is <0 or >1)throw new ArgumentOutOfRangeException(nameof(side));
        string? error=Validate(field,context);if(error is not null)return NativeAiResult<NativeAiField>.Unsupported(error);
        var state=field;
        for(int slot=0;slot<5;slot++)
        {
            var m=state.At(side,slot);
            if(m is null || !m.DefensePosition || (!m.FaceUp&&!context.FaceUpPlacementAllowed))continue;
            if(m.PositionChangePredicate is null)return NativeAiResult<NativeAiField>.Unsupported("005B09C0 position-change predicate is unresolved");
            if(m.PositionChangePredicate.Value) state=state.With(side,slot,m with{DefensePosition=false,FaceUp=true});
        }
        return NativeAiResult<NativeAiField>.Supported(state);
    }

    public static NativeAiResult<NativeAiSequenceEvaluation> Baseline(NativeAiField field,int side,
        NativeAiFieldContext context,ushort initialSummary=0)
    {
        var prepared=PrepareBaseline(field,side,context);
        return prepared.IsSupported ? Evaluate(prepared.Value!,side,context,initialSummary) :
            NativeAiResult<NativeAiSequenceEvaluation>.Unsupported(prepared.UnsupportedReasons.ToArray());
    }
}
