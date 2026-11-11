using Joey.Core;

internal static class AiSequenceChecks
{
    private static NativeAiMonster M(int attack,int defense=0, bool defending=false,bool faceUp=true,
        bool? positionAllowed=false,ushort resource=0x213,ushort index=550) =>
        new(new(index,18,5,attack,defense),resource,defending,faceUp,4,RuleFlags: positionAllowed is bool allowed ? (allowed ? 0u : 0x20000u) : null);
    public static void Run(Action<bool,string> check)
    {
        var context=NativeAiFieldContext.ResolvedPlain(8000,8000);
        var empty=NativeAiField.Empty;
        var unstable=NativeAiSequence.OrderAttackers([new(0,0x213,2000),new(1,0x213,2000),new(2,0x213,1000)]);
        check(unstable.SequenceEqual(new[]{2,1,0}),"Native selection-sort swaps reverse a prior equal-key pair");
        check(NativeAiSequence.OrderAttackers([new(0,0x2e8,500),new(1,0x213,2000),new(2,0x5f3,2500)])
            .SequenceEqual(new[]{2,1,0}),"Native attacker class dominates ATK in ordering");
        check(NativeAiSequence.OrderAttackers([new(0,0x213,1),new(1,0x213,65536)]).SequenceEqual(new[]{1,0}),
            "Native attack key uses the low16 ATK bits");
        var field=empty.With(0,0,M(1700)).With(0,1,M(1900)).With(1,0,M(1500)).With(1,1,M(0,1000,true));
        var value=NativeAiSequence.Evaluate(field,0,context).Value!;
        check(value.Score==200 && value.ActionMask==3 && value.OpponentLosses==2 && value.OwnLosses==0 &&
            value.OwnSurvivors==2 && value.OpponentSurvivors==0 && value.RankingSummary==144,
            "Sequence independently accumulates ATK kill plus zero-score DEF kill and summary");
        check(value.Events.Select(e=>Convert.ToHexString(e.ToBytes())).SequenceEqual(new[]{"0C08C800","9C080000"}),
            "Sequence emits chosen descriptors in native greedy order");
        check(field.Count(1)==2 && value.SimulatedField.Count(1)==0 && field.At(0,0)!.Stats.Attack==1700,
            "Hypothetical removals never mutate the caller's field");
        var direct=NativeAiSequence.Evaluate(empty.With(0,0,M(1700)).With(0,1,M(1900)).With(1,0,M(1500)),0,context).Value!;
        check(direct.Score==2100 && direct.ActionMask==3 && direct.Events[1].SpecialTarget &&
            Convert.ToHexString(direct.Events[1].ToBytes())=="16006C07",
            "Removing the last defender enables special target for the remaining attacker");
        var reconsider=NativeAiSequence.Evaluate(empty.With(0,0,M(1000)).With(0,1,M(2000)).With(1,0,M(1900)),0,context).Value!;
        check(reconsider.Score==1100 && reconsider.Events.Select(e=>e.AttackerSlot).SequenceEqual(new[]{1,0}),
            "A previously useless attacker is reconsidered after another attacker removes the target");
        var tie=NativeAiSequence.Evaluate(empty.With(0,0,M(1700)).With(1,0,M(1700)),0,context).Value!;
        check(tie.Score==0 && tie.ActionMask==0 && tie.Events.Count==0 && tie.SimulatedField.Count(0)==1,
            "Useless equal-ATK mutual destruction is not consumed by greedy sequence");
        var defended=empty.With(0,0,M(1700,1000,true,positionAllowed:true)).With(1,0,M(1000));
        check(NativeAiSequence.Evaluate(defended,0,context).Value!.Events.Count==0 &&
            NativeAiSequence.Baseline(defended,0,context).Value!.Score==700 && defended.At(0,0)!.DefensePosition,
            "Baseline prepares positions on local state and restores caller view by isolation");
        check(!NativeAiSequence.Baseline(empty.With(0,0,M(1700,1000,true,positionAllowed:null)),0,context).IsSupported,
            "Unresolved baseline position-change predicate remains unsupported");
        check(!NativeAiSequence.Evaluate(field,0,NativeAiFieldContext.Unknown).IsSupported &&
            !NativeAiSequence.Evaluate(empty.With(0,0,M(2300,resource:0x540)),0,context).IsSupported,
            "Unknown field rules and unsupported intrinsic cards are not empty effects");
        var targetTies=empty.With(0,0,M(2000)).With(1,0,M(1000,900)).With(1,1,M(1000,1900));
        check(NativeAiSequence.Evaluate(targetTies,0,context).Value!.Events[0].TargetSlot==1,
            "Equal scores prefer the new visible target only with greater real DEF");
        var hiddenTie=targetTies.With(1,1,M(9999,0,faceUp:false));
        check(NativeAiSequence.Evaluate(hiddenTie,0,context).Value!.Events[0].TargetSlot==1,
            "A new hidden target replaces an equal-score target without comparing DEF");
        var oldHidden=targetTies.With(1,0,M(9999,5000,faceUp:false)).With(1,1,M(1000,1000));
        check(NativeAiSequence.Evaluate(oldHidden,0,context).Value!.Events[0].TargetSlot==0,
            "A new visible target compares DEF against old hidden target's actual DEF");
        var heuristic=empty.With(0,0,M(1700,positionAllowed:true)).With(1,0,M(1000)).With(1,1,M(2800));
        check(NativeAiSequence.Evaluate(heuristic,0,context).Value!.Events.Count==0 &&
            NativeAiSequence.Evaluate(heuristic.With(0,0,M(1700,positionAllowed:false)),0,context).Value!.Score==700,
            "005B05A0 can reject a legal favorable target because another rival exceeds ATK+1000");
        var stopped=NativeAiSequence.Evaluate(field,0,NativeAiFieldContext.ResolvedPlain(8000,8000,flags0:0x40000),0xffff).Value!;
        check(stopped.Score==0 && stopped.Events.Count==0 && stopped.ScoreSummary==0xffc0 && stopped.RankingSummary==0xf480,
            "Battle gate clears loss fields while score summary preserves prior high bits");
        var ruleBlocked=NativeAiSequence.Evaluate(empty.With(0,0,M(1900) with{RuleFlags=0x40000}),0,context).Value!;
        check(ruleBlocked.Events.Count==0,"Native monster rule flag40000 independently prevents attack eligibility");
        var ignoredThreat=heuristic.With(1,1,M(2800) with{RuleFlags=0x40000});
        check(NativeAiSequence.Evaluate(ignoredThreat,0,context).Value!.Score==700,
            "005ADE40 ignores an opposing monster rejected by core attack eligibility");
        var unknownFlags=M(1900) with{RuleFlags=null,PositionChangeAllowed=true};
        check(!NativeAiSequence.Evaluate(empty.With(0,0,unknownFlags).With(1,0,M(1700)),0,context).IsSupported,
            "A resolved position predicate does not invent missing native rule flags");
        var contradictory=M(1900) with{RuleFlags=0,PositionChangeAllowed=false};
        check(!NativeAiSequence.Evaluate(empty.With(0,0,contradictory),0,context).IsSupported,
            "Contradictory raw flags and position predicate are rejected in the closed plain contract");
        check(NativeAiSequence.Evaluate(heuristic,0,NativeAiFieldContext.ResolvedPlain(8000,8000,flags1:0x40000)).Value!.Events.Count==0,
            "Opposing global battle-block flag does not erase ADE40 threats from core eligibility");
        check(NativeAiSequence.Evaluate(empty.With(0,0,M(1700) with{RuleFlags=0x20000}).With(1,0,M(1000)),0,context).Value!.Score==700,
            "Monster position-lock flag20000 does not prevent attacking");
        var ownOnly=empty.With(0,4,M(1000));
        var mirror=NativeAiSequence.Evaluate(empty.With(1,4,M(1000)),1,context).Value!;
        check(mirror.Score==1000 && mirror.Events[0].AttackerSlot==4 && mirror.Events[0].Flags==0x47,
            "Side1 retains native descriptor bit0 and nonzero attacker slot");
        var unknownPairs=NativeAiFieldContext.ResolvedPlain(8000,8000,pairContexts:
            new Dictionary<NativeAiAttackPair,NativeAiCombatContext>{{new(0,0,0),new()}});
        check(!NativeAiSequence.Evaluate(empty.With(0,0,M(1700)).With(1,0,M(1000)),0,unknownPairs).IsSupported,
            "Unknown pair context aborts aggregation instead of contributing zero");
        var own1=M(1000);var list=new NativeAiMonster?[]{own1,null,null,null,null};
        var immutable=new NativeAiField(list,new NativeAiMonster?[5]);list[0]=null;
        check(immutable.Count(0)==1,"Field constructor isolates caller-owned monster arrays");
    }
}
