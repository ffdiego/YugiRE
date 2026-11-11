using Joey.Core;

internal static class AiRankingChecks
{
    // Observed candidates and metrics, not a complete native legality model.
    private readonly record struct LiveCandidate(
        int HandIndex, int CardIndex, int Attack, int Score, int Slot,
        ushort ActionMask, ushort Summary);
    private readonly record struct LiveDecision(
        int CallId, int Pass, int Baseline, int InitialOpponentLosses,
        int ExpectedHandIndex, LiveCandidate[] Candidates);

    public static void Run(Action<bool,string> check)
    {
        // Independent branch-oriented oracle: CMP/JL, TEST/JZ, CMP/JLE,
        // JNZ, CMP/JGE, TEST/JA from ai-decision-selectors-deep.txt.
        bool BranchOracle(int best, int losses, int score, int attack, int slot, ushort mask, ushort aa)
        {
            if (attack < 0x384) return false;  // 005B154B
            if ((mask & (1 << slot)) == 0) return false; // 005B1565
            bool ebx = score > best;           // 005B156D..1571
            if (score != best) return ebx;      // JNZ 005B157B
            int ecx = (aa & 0xff) >> 3 & 7;    // 005B157D..1589
            if (losses >= ecx) return ebx;
            return (aa & 0x1c0) > 0 || ebx;    // 005B158D..159D
        }
        int cases = 0;
        foreach (int best in new[] {-400,0,200})
        foreach (int score in new[] {-401,-400,-1,0,1,199,200,201})
        foreach (int attack in new[] {0,899,900,1900})
        foreach (int slot in new[] {0,4})
        foreach (ushort mask in new ushort[] {0,1,16,31})
        foreach (int losses in new[] {0,1,5})
        foreach (ushort aa in new ushort[] {0,8,16,40,64,72,80,104,512,584,4095})
        {
            if (NativeAiRanking.AcceptNormalCandidate(best,losses,score,attack,slot,mask,aa) !=
                BranchOracle(best,losses,score,attack,slot,mask,aa)) throw new Exception("AI ranking diverges from assembly branches");
            cases++;
        }
        check(cases==25344,"AI final ranking agrees with assembly branch oracle for 25344 boundary combinations");
        check(!NativeAiRanking.AcceptNormalCandidate(0,0,0,1900,0,1,0x40),"AI tie without an additional opponent loss keeps prior choice");
        check(!NativeAiRanking.AcceptNormalCandidate(0,0,0,1900,0,1,8),"AI tie with no own survivor keeps prior choice");
        check(NativeAiRanking.AcceptNormalCandidate(0,0,0,900,4,16,72),"AI tie accepts more opponent losses with own survivors at ATK900");
        check(!NativeAiRanking.AcceptNormalCandidate(0,0,200,899,0,1,72),"AI score improvement cannot bypass ATK gate");
        check(!NativeAiRanking.AcceptNormalCandidate(0,0,200,1900,0,0,72),"AI score improvement cannot bypass acted-slot mask");
        check(NativeAiRanking.AcceptNormalCandidate(0,5,1,1900,0,1,0),"AI strict score improvement does not require tie counters");

        // Real Joey trace, 2026-11-11 UTC; SHA256 ea1ed03746df2fbd56f4ed9c2fd8abee7f502b2687a683c4597346183f7df449.
        // Only candidates actually evaluated by B1610 are included:
        // gates that excluded OTHER cards from the hand are not captured.
        // See docs/evidence/ai-detail-ranking-live-validation-2026-11-11.md.
        int firstSelection = -1, firstBestScore = 0, firstBestLosses = 0;
        bool accepted834 = NativeAiRanking.AcceptNormalCandidate(
            firstBestScore, firstBestLosses, 0, 1200, 0, 1, 72);
        if (accepted834)
        {
            firstSelection = 0;
            firstBestScore = 0;
            firstBestLosses = (72 >> 3) & 7;
        }
        bool accepted178First = NativeAiRanking.AcceptNormalCandidate(
            firstBestScore, firstBestLosses, 0, 1000, 0, 0, 576);
        if (accepted178First) firstSelection = 2;
        check(accepted834 && !accepted178First && firstSelection == 0,
            "Live selector 225: tie selects internal card 834 at hand index 0, not 178");

        bool accepted178Second = NativeAiRanking.AcceptNormalCandidate(
            0, 0, 1000, 1000, 0, 1, 64);
        check(accepted178Second,
            "Live selector 3379: score +1000 selects Karate Man at hand index 1");

        // Full native gate trace: SHA256 6c93df404998d8b0807ecfb98831a73db5485d274d0cfb29b5ae876a6d6fc5d3
        // See docs/evidence/ai-gates-full-decision-live-validation-2026-11-11.md.
        // AAB90/AE3E0/AE220 filtered 9 entries, pass=1 filtered 2 more;
        // the following table contains the FIFTEEN candidates actually
        // simulated by the original executable. Inputs are native outputs,
        // not C# computations of combat, stats or legality.
        LiveDecision[] decisions =
        {
            new(2954, -1, 0, 0, 0, new LiveCandidate[]
            {
                new(0, 24, 1700, 2300, 1, 3, 136),
                new(1, 857, 1700, 2300, 1, 3, 136),
                new(3, 550, 1900, 2300, 1, 3, 136),
            }),
            new(12651, 0, 2500, 1, 2, new LiveCandidate[]
            {
                new(0, 857, 1700, 4200, 2, 7, 200),
                new(2, 550, 1900, 4400, 2, 7, 200),
                new(3, 261, 1100, 3600, 2, 7, 200),
            }),
            new(31727, -1, 0, 0, 0, new LiveCandidate[]
            {
                new(0, 857, 1700, 1700, 0, 1, 64),
                new(2, 261, 1100, 1100, 0, 1, 64),
                new(3, 380, 1100, 1100, 0, 1, 64),
            }),
            new(33504, -1, 0, 1, 1, new LiveCandidate[]
            {
                new(1, 261, 1100, 1700, 1, 3, 136),
                new(2, 380, 1100, 1700, 1, 3, 136),
            }),
            new(37786, 1, 2800, 0, -1, Array.Empty<LiveCandidate>()),
            new(37800, -1, 2800, 0, 2, new LiveCandidate[]
            {
                new(1, 380, 1100, 3900, 2, 7, 192),
                new(2, 550, 1900, 4700, 2, 7, 192),
            }),
            new(39379, -1, 0, 0, 1, new LiveCandidate[]
            {
                new(1, 380, 1100, 1100, 0, 1, 64),
                new(2, 515, 2100, 0, 0, 0, 64),
            }),
        };

        int simulated = 0, accepted = 0, rejected = 0;
        foreach (var decision in decisions)
        {
            int bestIndex = -1;
            int bestScore = decision.Baseline;
            int bestLosses = decision.InitialOpponentLosses;
            foreach (var candidate in decision.Candidates)
            {
                bool take = NativeAiRanking.AcceptNormalCandidate(
                    bestScore, bestLosses, candidate.Score, candidate.Attack,
                    candidate.Slot, candidate.ActionMask, candidate.Summary);
                simulated++;
                if (take)
                {
                    accepted++;
                    bestIndex = candidate.HandIndex;
                    bestScore = candidate.Score;
                    bestLosses = (candidate.Summary >> 3) & 7;
                }
                else rejected++;
            }
            check(bestIndex == decision.ExpectedHandIndex,
                $"Live gate trace selector {decision.CallId}, pass {decision.Pass}: " +
                $"predicted hand index {bestIndex}, native {decision.ExpectedHandIndex}");
        }
        check(decisions.Length == 7 && simulated == 15 &&
              accepted == 8 && rejected == 7,
            "Live gate trace: 7/7 selector outputs and 15/15 ranked candidates");


    }
}
