using Joey.Core;

internal static class AiCandidateSearchChecks
{
    private static NativeAiNormalCandidate C(int hand,int attack,ushort resource=0x213,ushort index=550) =>
        new(hand,new(index,18,5,attack,900),resource,4);
    public static void Run(Action<bool,string> check)
    {
        var context=NativeAiFieldContext.ResolvedPlain(8000,8000);
        var empty=NativeAiField.Empty;
        var first=NativeAiCandidateSearch.SelectNormal(empty,0,-1,[C(0,1700,0x4d,24),C(1,1900)],context).Value!;
        check(first.HandIndex==1 && first.Baseline.Score==0 && first.Candidates.Select(c=>c.Evaluation!.Score).SequenceEqual(new[]{1700,1900}),
            "C# field-to-score-to-ranking selects independently generated1900 over1700");
        check(first.Candidates.All(c=>c.Accepted) && empty.Count(0)==0,
            "Candidate comparisons update best sequentially without mutating hand or field");
        var defender=new NativeAiMonster(new(24,15,5,0,1800),0x4d,DefensePosition:true,RuleFlags:0);
        var tied=NativeAiCandidateSearch.SelectNormal(empty.With(1,0,defender),0,-1,[C(0,1700,0x4d,24),C(1,1900)],context).Value!;
        check(tied.HandIndex==1 && tied.Candidates[0].Evaluation!.ActionMask==0 &&
            tied.Candidates[1].Evaluation!.Score==0 && tied.Candidates[1].Evaluation!.RankingSummary==72,
            "Independent score-zero free-kill sequence drives the native loss/survivor tie break");
        var stable=NativeAiCandidateSearch.SelectNormal(empty,0,0,[C(0,1700,0x4d,24),C(1,1700,0x4d,24)],context).Value!;
        check(stable.HandIndex==0 && !stable.Candidates[1].Accepted,
            "Equal independently computed scores and counters retain the first hand position");
        var tributePass=NativeAiCandidateSearch.SelectNormal(empty,0,1,[C(0,1700,0x4d,24),C(1,1900)],context).Value!;
        check(tributePass.HandIndex==-1 && tributePass.Candidates.All(c=>c.Evaluation is null),
            "Pass1 filters the supported level4 hand before hypothetical evaluation");
        var noSpace=NativeAiCandidateSearch.SelectNormal(empty,0,-1,[C(0,1900)],context,0).Value!;
        check(noSpace.HandIndex==-1 && noSpace.Candidates[0].Evaluation is null,
            "No permitted slot rejects a candidate without fabricating a score");
        var zeroAttack=NativeAiCandidateSearch.SelectNormal(empty,0,-1,[C(0,800)],context).Value!;
        check(zeroAttack.HandIndex==-1 && zeroAttack.Candidates[0].Reason=="Calculated ATK below900",
            "An independently positive sequence cannot bypass the normal ATK gate");
        var existing=new NativeAiMonster(new(550,18,5,1900,900),0x213,RuleFlags:0x20000);
        var improved=NativeAiCandidateSearch.SelectNormal(empty.With(0,0,existing),0,0,[C(0,1700,0x4d,24),C(1,1900)],context).Value!;
        check(improved.Baseline.Score==1900 && improved.HandIndex==1 && improved.Candidates[1].Evaluation!.Score==3800,
            "Existing field baseline and two-monster candidate aggregate are produced entirely by C#");
        check(!NativeAiCandidateSearch.SelectNormal(empty,0,-1,[C(0,2300,0x540,307)],context).IsSupported,
            "Unknown candidate policy never masquerades as rejection or zero contribution");
        check(!NativeAiCandidateSearch.SelectNormal(empty,0,-1,[C(0,1900)],NativeAiFieldContext.Unknown).IsSupported,
            "Unknown field context propagates through candidate search");
        var zone2=NativeAiCandidateSearch.SelectNormal(empty,0,-1,[C(0,1900)],context,4).Value!;
        check(zone2.HandIndex==0 && zone2.Candidates[0].Slot==2 && zone2.Candidates[0].Evaluation!.ActionMask==4,
            "Candidate hypothetical placement uses the first permitted empty native zone");
    }
    public static void RunCatalog(Action<bool,string> check,Archive archive)
    {
        var cards=Catalog.Load(archive);
        var hand=NativeAiCandidateSearch.FromCatalog(new[]{cards.Single(c=>c.Index==24),cards.Single(c=>c.Index==550)},
            NativeAiAttributeContext.Resolved());
        var choice=NativeAiCandidateSearch.SelectNormal(NativeAiField.Empty,0,-1,hand.Value!,
            NativeAiFieldContext.ResolvedPlain(8000,8000));
        check(hand.IsSupported && choice.IsSupported && choice.Value!.HandIndex==1 &&
            choice.Value.Candidates[0].Evaluation!.Score==1700 && choice.Value.Candidates[1].Evaluation!.Score==1900,
            "Pinned DAT →computed attributes→local hypothesis→combat→score→ranking works without native intermediate metrics");
        check(!NativeAiCandidateSearch.FromCatalog([cards.Single(c=>c.Index==550)]).IsSupported,
            "Catalog data does not prove absence of field/equipment modifiers");
    }
}
