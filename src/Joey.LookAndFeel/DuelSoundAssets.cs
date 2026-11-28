namespace Joey.LookAndFeel;

public enum DuelSoundCue
{
    Start, TurnChange, BattlePhase, CardMove, Activation, Attack,
    Destruction, Tribute, SpecialSummon, Damage, Heal, FusionStart, FusionResult, DiceBounce
}

/// <summary>Original DAT filenames with provisional semantic associations,
/// NOT a mapping of native descriptor IDs or physical archive order.</summary>
public static class DuelSoundAssets
{
    public const string InternalPrefix=@"joey\english\internal\";
    public const string StreamPrefix=@"joey\english\stream\";
    public static string Effect(DuelSoundCue cue) => InternalPrefix+(cue switch
    {
        DuelSoundCue.Start=>"start.wav",
        DuelSoundCue.TurnChange=>"turn_change.wav",
        DuelSoundCue.BattlePhase=>"battle_turn.wav",
        DuelSoundCue.CardMove=>"card_move.wav",
        DuelSoundCue.Activation=>"magic_active.wav",
        DuelSoundCue.Attack=>"attack.wav",
        DuelSoundCue.Destruction=>"card_attacked_brake.wav",
        DuelSoundCue.Tribute=>"ikenie.wav",
        DuelSoundCue.SpecialSummon=>"reborn.wav",
        DuelSoundCue.Damage=>"damage.wav",
        DuelSoundCue.Heal=>"life_up.wav",
        DuelSoundCue.FusionStart=>"fusion1.wav",
        DuelSoundCue.FusionResult=>"fusion2.wav",
        DuelSoundCue.DiceBounce=>"dice.wav",
        _=>throw new ArgumentOutOfRangeException(nameof(cue))
    });

    public static string? Music(string screen) => screen switch
    {
        "menu" or "cards" or "deck"=>StreamPrefix+"m_menu.wav",
        "duel"=>StreamPrefix+"m_duel1.wav",
        _=>null
    };
}
