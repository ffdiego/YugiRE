"""Validate external card-text hypotheses without treating them as Joey rules.

Names/IDs from native catalog are checked together to prevent accidental joins
to modern web card IDs. No web requests. Entries are short paraphrases with
provenance, not bulk copies of copyrighted descriptions.
"""
from __future__ import annotations


def validate_expectations(reference: dict, catalog_cards: list[dict]) -> tuple[dict, list[dict]]:
    if not isinstance(reference, dict) or reference.get("schemaVersion") != 1 or (
        reference.get("kind") != "ExternalCardTextExpectations"
    ):
        raise ValueError("Expected ExternalCardTextExpectations v1")
    by_index = {c["internalIndex"]: c for c in catalog_cards}
    seen = set()
    annotations = {}
    for entry in reference.get("cards", []):
        idx, rid, name = entry["internalIndex"], entry["resourceId"], entry["name"]
        if idx in seen:
            raise ValueError(f"Duplicate card-text internal index {idx}")
        seen.add(idx)
        native = by_index.get(idx)
        if (native is None or native["resourceId"] != rid or
            native["name"].casefold() != name.casefold() or
            not native["joeyAvailable"]):
            raise ValueError(f"Card-text identity or Joey pack mismatch: {idx}/{rid} {name}")
        source = entry.get("source")
        if (not isinstance(source, dict) or
            source.get("kind") != "OfficialTCGCardDatabase" or
            not isinstance(source.get("url"), str) or
            not source["url"].startswith("https://www.db.yugioh-card.com/")):
            raise ValueError(f"Invalid official card-text provenance: {idx}/{rid}")
        if (not isinstance(entry.get("summaryPt"), str) or
            not entry["summaryPt"].strip() or
            not isinstance(entry.get("expectedMechanics"), list) or
            not entry["expectedMechanics"]):
            raise ValueError(f"Missing card-text summary/mechanic: {idx}/{rid}")
        annotations[idx] = entry

    hypotheses = []
    seen_hypothesis = set()
    for h in reference.get("interactionHypotheses", []):
        hid = h.get("id")
        refs = h.get("sourceCardIndices")
        if (not isinstance(hid, str) or not hid or hid in seen_hypothesis or
            not isinstance(refs, list) or len(refs) < 2 or
            len(set(refs)) != len(refs) or any(i not in annotations for i in refs) or
            h.get("conclusion") != "Unverified" or
            not isinstance(h.get("requiredNativeEvidence"), list) or
            not h["requiredNativeEvidence"]):
            raise ValueError(f"Invalid unverified interaction hypothesis {hid}")
        seen_hypothesis.add(hid)
        hypotheses.append(h)
    return annotations, hypotheses


def temporal_candidates(hypotheses: list[dict], annotated: dict,
                        examples_by_resource: dict) -> list[dict]:
    """CF ordering within a duel: leads for investigation, never active-state proof."""
    results = []
    for hypothesis in hypotheses:
        refs = hypothesis["sourceCardIndices"]
        # Interactions with >2 cards are preserved but not automatically
        # joined unless more evidence than these two simple CF sequences exists.
        pairs = []
        if len(refs) == 2:
            card_a, card_b = [annotated[i] for i in refs]
            a_cf = [e for e in examples_by_resource.get(card_a["resourceId"], [])
                    if e["kind"] == "CF"]
            b_cf = [e for e in examples_by_resource.get(card_b["resourceId"], [])
                    if e["kind"] == "CF"]
            for first in a_cf:
                for second in b_cf:
                    if (first["file"] == second["file"] and
                        first["duelIndex"] == second["duelIndex"]):
                        pairs.append(dict(file=first["file"], duelIndex=first["duelIndex"],
                                          cardASeq=first["seq"], cardBSeq=second["seq"],
                                          ordering="A_before_B" if first["seq"] < second["seq"]
                                          else "B_before_A"))
        results.append(dict(id=hypothesis["id"],
                            summaryPt=hypothesis["summaryPt"],
                            sourceCardIndices=refs,
                            requiredNativeEvidence=hypothesis["requiredNativeEvidence"],
                            conclusion="Unverified",
                            observationClass="CFTemporalCooccurrenceOnly" if pairs
                                             else "NoPairOfCFCandidates",
                            candidatePairs=pairs,
                            limitation="Commands are not proof of a trap remaining active, "
                                       "the spell being sent to the Graveyard, card ownership "
                                       "or the cause of any 300-LP loss."))
    return results
