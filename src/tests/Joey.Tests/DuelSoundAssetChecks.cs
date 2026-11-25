using Joey.LookAndFeel;

internal static class DuelSoundAssetChecks
{
    internal static void Run(Action<bool,string> check)
    {
        check(Enum.GetValues<DuelSoundCue>().Select(DuelSoundAssets.Effect).Distinct().Count()==11 &&
            Enum.GetValues<DuelSoundCue>().All(c=>DuelSoundAssets.Effect(c).StartsWith(DuelSoundAssets.InternalPrefix)),
            "V18: distinct named effect resources");
        check(DuelSoundAssets.Effect(DuelSoundCue.Activation).EndsWith("magic_active.wav") &&
            DuelSoundAssets.Effect(DuelSoundCue.Damage).EndsWith("damage.wav") &&
            DuelSoundAssets.Effect(DuelSoundCue.Heal).EndsWith("life_up.wav"),
            "V18: semantic resource adapter");
        check(DuelSoundAssets.Music("duel")==DuelSoundAssets.StreamPrefix+"m_duel1.wav" &&
            DuelSoundAssets.Music("cards")==DuelSoundAssets.Music("menu") &&
            DuelSoundAssets.Music("coin-review") is null,
            "V18: screen music, not isolated reviews");
    }
}
