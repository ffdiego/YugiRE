#!/usr/bin/env python3
"""Reconstruct the candidate initial deck order for version-1300 YDR files.
Static loader contract and existing native MSVC shuffle reconstruction.
No claim of full replay parity. No original assets or save identifier in output.
"""
from __future__ import annotations
import argparse
import json
from pathlib import Path
from inspect_joey_replay import read_file


def reconstruct_opening(summary: dict) -> dict:
    header = summary["header"]["header_fields_uninterpreted"]
    version, association, first, seed = (header[x] for x in ("0x0C","0x10","0x14","0x18"))
    if version != 1300 or association not in (0,1) or first not in (0,1):
        raise ValueError("unsupported replay version/selectors; only observed high-mode 1300 is enabled")
    if type(seed) is not int or not 0 <= seed <= 0xFFFFFFFF:
        raise ValueError("invalid seed")
    decks = summary["deck_groups"]
    if len(decks)!=2:
        raise ValueError("expected two serialized decks")
    result = {}
    for side, entry in enumerate(decks):
        groups=entry["groups"]
        if len(groups)!=3 or entry["side_index"]!=side:
            raise ValueError("unexpected deck group layout")
        for i,group in enumerate(groups):
            ids=group["resource_ids"]
            if group["index"]!=i or group["count"]!=len(ids) or len(ids)>(80 if i==0 else 15) or any(
                type(v) is not int or not 0<v<4000 for v in ids):
                raise ValueError(f"invalid side {side} group {i}")
        if len(groups[0]["resource_ids"])<5:
            raise ValueError("opening requires at least five main-deck cards")
        result[str(side)]=list(groups[0]["resource_ids"])
    state=seed
    for side in (first,1-first):
        deck=result[str(side)]
        for remaining in range(len(deck),0,-1):
            state=(state*0x343FD+0x269EC3)&0xFFFFFFFF
            rnd=(state>>16)&0x7FFF
            index=rnd*remaining//32767
            if index>=remaining:
                raise ValueError("native rand==32767 boundary unresolved; refuse reconstruction")
            deck[remaining-1],deck[index]=deck[index],deck[remaining-1]
    return {"version":version,"association_selector":association,"shuffle_first_side":first,
            "seed":seed,"main_deck_order":result,
            "group2_inventory":{str(i):list(d["groups"][2]["resource_ids"]) for i,d in enumerate(decks)},
            "group1_inventory":{str(i):list(d["groups"][1]["resource_ids"]) for i,d in enumerate(decks)},
            "status":"statically_reconstructed_not_runtime_verified"}


def initial_prefix_checks(records: list[dict], reconstruction: dict) -> list[dict]:
    """Check only a narrow opening prefix: 14/10, opening draws, first turn,
    ordinary draws, phase markers, 0D and hand placements. Stop on anything
    else; no silent skipping of unknown state mutations.
    0D is skipped ONLY in the observed first-turn prefix, as a diagnostic;
    this is not a replay executor or proof that 0D has no state effects.
    """
    if len(records)<4: return []
    raw=lambda r:int(r["raw_opcode_hex"],16)
    args=lambda r:tuple(r[f"argument_{i}"] for i in (1,2,3))
    if [raw(r) for r in records[:2]]!=[0x14,0x10] or any(args(r)!=(0,0,0) for r in records[:2]):
        return []
    first=reconstruction["shuffle_first_side"]
    if [raw(r) for r in records[2:4]]!=[0x61|(first<<15),0x61|((1-first)<<15)] or any(
        args(r)!=(0,5,0) for r in records[2:4]):
        return []
    decks={int(k):list(v) for k,v in reconstruction["main_deck_order"].items()}
    hands={s:decks[s][:5] for s in (0,1)}
    for s in (0,1): del decks[s][:5]
    checks=[]
    turns=0
    for r in records[4:]:
        word=raw(r)
        if word&0x7000: break
        op,side=word&0xFFF,word>>15
        a,b,c=args(r)
        if op==1:
            turns+=1
            if turns>1: break
        elif op==0x61 and (a,b,c)==(1,1,0) and decks[side]:
            hands[side].append(decks[side].pop(0))
        elif op in (0xCE,0xCF):
            index=b&0xFF
            expected=hands[side][index] if index<len(hands[side]) else None
            checks.append({"record_index":r["record_index"],"side":side,"hand_index":index,
                           "recorded_resource":a,"predicted_resource":expected,"match":expected==a})
            if expected!=a: break
            hands[side].pop(index)
        elif op in (0x50,0x51,0x52,0x55,0x0D):
            continue
        else: break
    return checks


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("replay",type=Path)
    args=p.parse_args()
    try:
        records,summary=read_file(args.replay)
        report=reconstruct_opening(summary)
        report["opening_placement_checks"]=initial_prefix_checks(records,report)
        print(json.dumps(report,indent=2))
    except (OSError,ValueError,KeyError,TypeError) as exc:
        p.exit(2,f"replay opening: {exc}\n")


if __name__=="__main__": main()
