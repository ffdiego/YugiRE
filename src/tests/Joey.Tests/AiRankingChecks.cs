using Joey.Core;

internal static class AiRankingChecks
{
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

    }
}
