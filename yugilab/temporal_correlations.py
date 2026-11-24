"""Descriptive, noncausal temporal links from validated native state transitions.

This deliberately recognizes only unit graveyard-count increases followed by
a -300 LP delta on the same side, within a bounded native sequence gap.
It does NOT identify the sent card, source of damage, trap activity, or
normalize historical timing across different Joey executions.
"""
from __future__ import annotations

def correlate_graveyard_lp300(transitions: list[dict], max_seq_gap: int = 6) -> dict:
    if type(max_seq_gap) is not int or not 1 <= max_seq_gap <= 32:
        raise ValueError("max_seq_gap must be 1..32")
    graves = sorted(
        (t for t in transitions if t.get("kind") == "graveyardCount" and
         t.get("delta") == 1),
        key=lambda t: (t["duelIndex"], t["seq"], t["side"]))
    losses = sorted(
        (t for t in transitions if t.get("kind") == "lifePoints" and
         t.get("delta") == -300),
        key=lambda t: (t["duelIndex"], t["seq"], t["side"]))
    used_losses: set[int] = set()
    paired, unpaired_graves = [], []
    for grave in graves:
        match = next((
            (i, loss) for i, loss in enumerate(losses)
            if i not in used_losses and
            loss["duelIndex"] == grave["duelIndex"] and
            loss["side"] == grave["side"] and
            0 < loss["seq"] - grave["seq"] <= max_seq_gap
        ), None)
        if match is None:
            unpaired_graves.append(dict(
                duelIndex=grave["duelIndex"], side=grave["side"],
                graveyardSeq=grave["seq"]))
            continue
        i, loss = match
        used_losses.add(i)
        paired.append(dict(
            duelIndex=grave["duelIndex"], side=grave["side"],
            graveyardSeq=grave["seq"], lossSeq=loss["seq"],
            seqGap=loss["seq"] - grave["seq"], lossDelta=-300,
            relation="SameSideCountIncreaseThenLpLoss",
            conclusion="TemporalCorrelationOnly"))
    unpaired_losses = [
        dict(duelIndex=loss["duelIndex"], side=loss["side"],
             lossSeq=loss["seq"], lossDelta=-300)
        for i, loss in enumerate(losses) if i not in used_losses
    ]
    return dict(
        maxSeqGap=max_seq_gap,
        selectionRule="First unused future -300 LP delta on the same side "
                      "within maxSeqGap after a +1 graveyard-count observation",
        unitGraveyardIncreases=len(graves), lpLoss300Count=len(losses),
        pairedCount=len(paired), pairs=paired,
        unpairedGraveyardIncreases=unpaired_graves,
        unpairedLpLoss300=unpaired_losses,
        limitation="Post-hoc window selection is descriptive, not independent "
                   "mechanical validation. Correlation does not show WHICH "
                   "card went to the Graveyard, whether Skull Invitation was "
                   "face-up or active, or which card/effect caused damage. "
                   "Observation seq is not an atomic game transaction.")
