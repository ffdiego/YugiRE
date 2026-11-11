namespace Joey.Core;

/// <summary>Full supplied hand for the supported plain subset; no native score input.</summary>
public sealed record NativeAiNormalCandidate(int HandIndex,NativeAiStats Stats,ushort ResourceId,int Level);
public sealed record NativeAiCandidateEvaluation(int HandIndex,int CardIndex,int? Slot,bool Accepted,
    string Reason,NativeAiSequenceEvaluation? Evaluation);
public sealed record NativeAiNormalChoice(int HandIndex,NativeAiSequenceEvaluation Baseline,
    IReadOnlyList<NativeAiCandidateEvaluation> Candidates);

/// <summary>Closed low-level 005B1360 subset. Eligibility/resource/special gates are
/// proven for Axe Raider/Gemini Elf under the resolved plain-field contract.
/// Tributes, non-plain cards and residual metadata in empty native slots are outside
/// the supported contract; no caller-side execution or normal-summon consumption.</summary>
public static class NativeAiCandidateSearch
{
    public static NativeAiResult<NativeAiNormalChoice> SelectNormal(NativeAiField field,int side,int pass,
        IEnumerable<NativeAiNormalCandidate> hand,NativeAiFieldContext context,ushort allowedZoneMask=0x1f)
    {
        if(side is <0 or >1)throw new ArgumentOutOfRangeException(nameof(side));
        if(allowedZoneMask>0x1f)throw new ArgumentOutOfRangeException(nameof(allowedZoneMask));
        var candidates=hand.ToArray();
        if(!candidates.Select(c=>c.HandIndex).SequenceEqual(Enumerable.Range(0,candidates.Length)))
            throw new ArgumentException("Supply the full hand in contiguous native hand-index order");
        var baseline=NativeAiSequence.Baseline(field,side,context);
        if(!baseline.IsSupported)return NativeAiResult<NativeAiNormalChoice>.Unsupported(baseline.UnsupportedReasons.ToArray());
        int best=-1,bestScore=baseline.Value!.Score,bestLosses=baseline.Value.OpponentLosses;
        var results=new List<NativeAiCandidateEvaluation>();
        foreach(var candidate in candidates)
        {
            if(candidate.ResourceId is not(0x4d or 0x213) || candidate.Level!=4 || candidate.Stats.CardIndex is 0 or >0xfff)
                return NativeAiResult<NativeAiNormalChoice>.Unsupported(
                    $"Eligibility/resource/tribute policy for hand{candidate.HandIndex},resource0x{candidate.ResourceId:X4},level{candidate.Level} is unsupported");
            int slot=Enumerable.Range(0,5).FirstOrDefault(i=>(allowedZoneMask&(1<<i))!=0 && field.At(side,i) is null,-1);
            if(slot<0) {results.Add(new(candidate.HandIndex,candidate.Stats.CardIndex,null,false,"No permitted empty monster zone",null));continue;}
            // For these ordinary level4 resources AE3E0(mode2) and AE220 are false.
            // No unimplemented gate is replaced by true for another resource.
            if(pass==1) {results.Add(new(candidate.HandIndex,candidate.Stats.CardIndex,null,false,"Pass1 excludes level1..4",null));continue;}
            var prepared=NativeAiSequence.PrepareBaseline(field,side,context);
            if(!prepared.IsSupported)return NativeAiResult<NativeAiNormalChoice>.Unsupported(prepared.UnsupportedReasons.ToArray());
            // Closed subset: empty zones have cleared residual rule flags. B1610
            // inserts face-up attack identity; it does not debit hand/LP/deck.
            var monster=new NativeAiMonster(candidate.Stats,candidate.ResourceId,Level:candidate.Level,RuleFlags:0);
            var hypothetical=prepared.Value!.With(side,slot,monster);
            var evaluation=NativeAiSequence.Evaluate(hypothetical,side,context);
            if(!evaluation.IsSupported)return NativeAiResult<NativeAiNormalChoice>.Unsupported(evaluation.UnsupportedReasons.ToArray());
            var value=evaluation.Value!;
            bool accepted=NativeAiRanking.AcceptNormalCandidate(bestScore,bestLosses,value.Score,
                candidate.Stats.Attack,slot,value.ActionMask,value.RankingSummary);
            string reason=accepted ? "Native final comparator accepted" : candidate.Stats.Attack<900 ?
                "Calculated ATK below900" : (value.ActionMask&(1<<slot))==0 ? "Candidate slot absent from acted mask" :
                "Score/tie counters did not improve the current best";
            results.Add(new(candidate.HandIndex,candidate.Stats.CardIndex,slot,accepted,reason,value));
            if(accepted){best=candidate.HandIndex;bestScore=value.Score;bestLosses=value.OpponentLosses;}
        }
        return NativeAiResult<NativeAiNormalChoice>.Supported(new(best,baseline.Value!,Array.AsReadOnly(results.ToArray())));
    }

    /// <summary>DAT-backed supported hand. Unknown attribute contexts propagate, never
    /// converted to zero stats. This obtains all candidate stats without Joey.</summary>
    public static NativeAiResult<IReadOnlyList<NativeAiNormalCandidate>> FromCatalog(
        IEnumerable<Card> cards,NativeAiAttributeContext? attributeContext=null)
    {
        var result=new List<NativeAiNormalCandidate>();
        foreach(var card in cards)
        {
            var stats=NativeAiAttributes.FromPlainCard(card,attributeContext);
            if(!stats.IsSupported)return NativeAiResult<IReadOnlyList<NativeAiNormalCandidate>>.Unsupported(stats.UnsupportedReasons.ToArray());
            result.Add(new(result.Count,stats.Value,card.Id,card.Level));
        }
        return NativeAiResult<IReadOnlyList<NativeAiNormalCandidate>>.Supported(Array.AsReadOnly(result.ToArray()));
    }
}
