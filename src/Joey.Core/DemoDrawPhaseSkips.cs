namespace Joey.Core;

public sealed partial class DemoDuel
{
    // A single due turn per player, not a number of draws to suppress.
    // Several effects that skip the same next Draw Phase coalesce.
    private readonly int?[] skippedDrawPhaseDue = new int?[2];

    public int? PendingSkippedDrawTurn(int side) => side is 0 or 1
        ? skippedDrawPhaseDue[side] : throw new ArgumentOutOfRangeException(nameof(side));

    private void ScheduleNextDrawPhaseSkip(int side)
    {
        // Current supported activations are on the caster's own Main Phase.
        // Side-aware formula also describes the next owner turn.
        skippedDrawPhaseDue[side] = checked(Turn + (side == ActivePlayer ? 2 : 1));
    }

    private bool ConsumeDueDrawPhaseSkip()
    {
        if (skippedDrawPhaseDue[ActivePlayer] != Turn) return false;
        skippedDrawPhaseDue[ActivePlayer] = null;
        return true;
    }

    // The sandbox allows ending a turn without using its explicit draw
    // action. A skip applies to that turn, never a later clicked draw.
    private void ExpireDrawPhaseSkipOnTurnExit()
    {
        if (skippedDrawPhaseDue[ActivePlayer] == Turn)
            skippedDrawPhaseDue[ActivePlayer] = null;
    }
}
