#!/usr/bin/env python3
"""Read-only S3 structural audit and path-sensitive F40 diagnostic.

Uses observed native stats/predicates, never computes independent attributes.
Missing required calls fail; skipped calls are explicitly recorded, not zeroed.
"""
from __future__ import annotations
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import struct

from ai_decision_trace_audit import audit as audit_selectors

EXPECTED_EXE = 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de'
SERVICES = {
    'own_stats': (0x5ADD60, {0x5B0F97}),
    'opponent_stats': (0x5ADDC0, {0x5B0FAE}),
    'equipment_count': (0x56D790, {0x5B1060}),
    'context_count': (0x56D8D0, {0x5B1117, 0x5B112F, 0x5B1153, 0x5B116F}),
    'field_query': (0x5699A0, {0x5B11F4, 0x5B1227}),
    'phase_query': (0x56B220, {0x5B1205, 0x5B1238}),
    'resource': (0x4022E0, {0x5B0FB8, 0x5B1009, 0x5B10FD, 0x5B1251}),
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def i32(n):
    return (n + 2**31) % 2**32 - 2**31


def half(n):
    n = i32(n * 5 + 5)
    return n // 10 if n >= 0 else -((-n) // 10)


def diagnostic(entry, children):
    """Consume exact ordered natural calls; outputs never decide missing inputs."""
    pending = iter(children)
    decisions = []

    def take(name, caller, args):
        event = next(pending, None)
        require(event is not None, f'Missing required {name} at {caller:x}')
        require(event['function'] == name and int(event['caller'], 16) == caller,
                f'Unexpected call at seq {event["seq"]}; expected {name}/{caller:x}')
        actual = event['argsRaw']
        require(len(actual) == len(args) and all(want is None or got == want
                for got, want in zip(actual, args)), f'Arguments disagree at seq {event["seq"]}')
        return event

    def predicate(name, caller, args):
        event = take(name, caller, args)
        raw, signed = event['returnRaw'], event['returnSigned']
        require(0 <= raw <= 0xFFFFFFFF and signed == i32(raw), 'Invalid EAX representation')
        return signed

    def resource(caller, stats):
        event = take('resource', caller, [None])
        # 004022E0 consumes ushort; F40 pushes all four stat-header bytes.
        require(event['argsRaw'][0] == int.from_bytes(stats[:4], 'little'), 'Resource input header mismatch')
        require(event['resourceId'] == event['returnRaw'] & 0xFFFF, 'Resource low16 mismatch')
        return event['resourceId']

    side, a_slot, d_slot = entry['argsRaw']
    own = bytes.fromhex(take('own_stats', 0x5B0F97, [side, a_slot, None])['outputHex'])
    enemy = bytes.fromhex(take('opponent_stats', 0x5B0FAE, [1-side, d_slot, None])['outputHex'])
    a_index, a_type, _, attack, defense = struct.unpack('<HBBii', own)
    d_index, d_type, d_attr, enemy_attack, enemy_defense = struct.unpack('<HBBii', enemy)
    require(a_index == entry['attacker']['internalCardIndex'], 'Own identity mismatch')
    if entry['defender']['faceUp']:
        require(d_index == entry['defender']['internalCardIndex'], 'Visible enemy identity mismatch')
    else:
        require(enemy[:4] == b'\0'*4 and enemy_attack == enemy_defense and enemy_attack in (1000,1900),
                'Invalid hidden estimate layout')
    a_resource = resource(0x5B0FB8, own)
    d_resource = resource(0x5B1009, enemy)
    if a_resource == 0x1DD and d_type == 6:
        attack = i32(attack+1000)
    elif a_resource == 0x4E5 and d_type == 15:
        attack, defense = i32(attack+2000), i32(defense+2000)
    if d_resource == 0x11F and a_type == 1:
        enemy_defense = half(enemy_defense)
    elif d_resource == 0x4E5 and a_type == 15:
        enemy_attack, enemy_defense = i32(enemy_attack+2000), i32(enemy_defense+2000)
    count = predicate('equipment_count', 0x5B1060, [side,a_slot,0x291])
    require(count >= 0, 'Negative equipment count')
    attack = i32(attack + i32(count*half(enemy_attack)))
    posture = entry['defender']['defensePosition']
    flags = (int.from_bytes(bytes.fromhex(entry['seedHex'])[:2], 'little') & 0xF005) | a_slot<<4 | d_slot<<7
    gain = loss = 0
    opposing = enemy_defense if posture else enemy_attack
    if attack == opposing:
        if not posture and attack > 0:
            flags |= 0xC00
    elif attack > opposing:
        flags |= 0x800
        difference = i32(attack-opposing)
        if not posture:
            gain = difference
        else:
            require(resource(0x5B10FD, own) == a_resource, 'Resource changed within F40')
            if a_resource == 0x53D:
                gain = difference
                decisions.append('pierce-own:intrinsic-053D;both-counts-not-evaluated')
            else:
                first = predicate('context_count', 0x5B1117, [side,a_slot,0x521,side])
                if first != 0:
                    gain = difference
                    decisions.append('pierce-own:521-true;604-not-evaluated')
                elif predicate('context_count', 0x5B112F, [side,a_slot,0x604,side]) != 0:
                    gain = difference
            first = predicate('context_count', 0x5B1153, [side,a_slot,0x521,1-side])
            if first != 0:
                loss = difference
                decisions.append('pierce-other:521-true;604-not-evaluated')
            elif predicate('context_count', 0x5B116F, [side,a_slot,0x604,1-side]) != 0:
                loss = difference
    else:
        loss = i32(opposing-attack)
        if not posture:
            flags |= 0x400
    if not (posture and attack > opposing):
        decisions.append('pierce:branch-not-evaluated')
    # These two flags are ENTRY samples, not observed again at native use sites.
    if entry['defender']['playerFlagsRaw'] & 0x400000:
        flags &= 0xF7FF
        gain = 0
    if entry['attacker']['playerFlagsRaw'] & 0x400000:
        flags &= 0xFBFF
        loss = 0
    for direction, amount, f_return, p_return, target_side in (
        ('gain',gain,0x5B11F4,0x5B1205,1-side),
        ('loss',loss,0x5B1227,0x5B1238,side)):
        if amount <= 0:
            decisions.append(direction+':nonpositive;field-and-phase-not-evaluated')
            continue
        found = predicate('field_query', f_return, [target_side,0x58F])
        if found <= 0:
            decisions.append(direction+':field-nonpositive;phase-not-evaluated')
        elif predicate('phase_query', p_return, [0x14D]) != 0:
            if direction == 'gain':
                gain = 0
            else:
                loss = 0
    require(resource(0x5B1251, own) == a_resource, 'Resource changed within F40')
    if a_resource == 0x199 and d_attr == 2:
        flags |= 0x800
    elif a_resource == 0x4CF:
        flags &= 0xFBFF
        loss, gain = 0, 0 if posture else 500
    elif a_resource == 0x4E3 and enemy_attack >= 1900:
        flags &= 0xFBFF
    require(next(pending, None) is None, 'Unexpected extra service call')
    score = (gain-loss+32768) % 65536-32768
    useful = score > 0 or (flags & 0x800) > ((flags & 0x400)<<1)
    flags = flags & 0xFFF7 | (8 if useful else 0)
    return struct.pack('<Hh',flags,score).hex().upper(), decisions, {
        'attackerIndex':a_index, 'attackerResource':a_resource,
        'effectiveDefenderIndex':d_index, 'effectiveDefenderResource':d_resource,
        'hiddenEstimate':not entry['defender']['faceUp'],
    }


def audit(records):
    selector_audit = audit_selectors(records)
    require(not selector_audit['readFailures'], 'Selector read failure')
    metadata, events = records[0], records[1:]
    selector_stacks, combat_stacks = defaultdict(list), defaultdict(list)
    frames, completed, ids = {}, [], set()
    for event in events:
        require(not any(value for key, value in event.items()
                        if 'error' in key.lower() or 'overflow' in key.lower()), 'Recorded error/overflow')
        if event['kind'] == 'ai_probe' and event['function'] in ('select_normal','select_priority'):
            stack = selector_stacks[event['threadId']]
            if event['phase'] == 'enter':
                stack.append(event['callId'])
            else:
                require(stack and stack.pop() == event['callId'], 'Selector mismatch')
        if event['kind'] != 'combat_context':
            continue
        require(event['schemaVersion'] == 1 and event['session'] == metadata['session'], 'Schema/session mismatch')
        key = (event['session'],event['threadId'],event['combatCallId'])
        stack = combat_stacks[event['threadId']]
        require(selector_stacks[event['threadId']] and
                event['selectorCallId'] == selector_stacks[event['threadId']][-1], 'Wrong selector parent')
        if event['function'] == 'combat_event' and event['phase'] == 'enter':
            require(key not in frames and event['callId'] not in ids, 'Duplicate combat identity')
            ids.add(event['callId'])
            side,a,d = event['argsRaw']
            require(side in (0,1) and 0 <= a < 5 and 0 <= d < 5, 'Invalid side/slot')
            for role,field_side,slot in (('attacker',side,a),('defender',1-side,d)):
                snap = event[role]
                require(snap['side'] == field_side and snap['slot'] == slot, 'Slot identity mismatch')
                require(snap['internalCardIndex'] == snap['identityRaw'] & 0xFFF, 'Index mismatch')
                require(snap['defensePosition'] == bool(snap['positionRaw'] & 1) and
                        snap['faceUp'] == bool(snap['positionRaw'] & 2), 'Posture decode mismatch')
                require(isinstance(snap['playerFlagsRaw'],int), 'Unknown player flags')
            require(int(event['functionVa'],16) == 0x5B0F40 and int(event['caller'],16) == 0x5B0E1C,
                    'Unreviewed F40 caller/base')
            frames[key] = [event,[]]
            stack.append(key)
        else:
            require(stack and stack[-1] == key, 'Orphan/cross-thread combat child')
            entry,children = frames[key]
            if event['function'] == 'combat_event':
                require(event['phase'] == 'leave' and event['callId'] == entry['callId'], 'Wrong F40 leave')
                require(event['childCount'] == len(children), 'Child count mismatch')
                predicted, decisions, inputs = diagnostic(entry,children)
                completed.append({'combatCallId':entry['callId'], 'selectorCallId':entry['selectorCallId'],
                    'enterSeq':entry['seq'], 'leaveSeq':event['seq'],
                    'observedHex':event['outputHex'], 'diagnosticHex':predicted,
                    'match':predicted == event['outputHex'].upper(), 'pathDecisions':decisions, **inputs})
                stack.pop()
            else:
                require(event['callId'] not in ids, 'Duplicate child callId')
                ids.add(event['callId'])
                target,callers = SERVICES[event['function']]
                require(event['phase'] == 'leave' and int(event['functionVa'],16) == target and
                        int(event['caller'],16) in callers, 'Wrong service caller/VA')
                children.append(event)
    require(not any(combat_stacks.values()), 'Unclosed F40')
    context_events = [e for e in events if e['kind'] == 'combat_context']
    return {
        'scope':'path-sensitive diagnostic with observed native stats/predicates; entry flags assumed stable',
        'metadataExeSha256':metadata.get('exeSha256'),
        'exeAttestedInJsonl':metadata.get('exeSha256') == EXPECTED_EXE,
        'events':len(events), 'sequenceIntegrity':'contiguous; metadata counts agree; no recorded errors',
        'selectorPairs':selector_audit['normalCount']+selector_audit['priorityCount'],
        'functions':dict(Counter(e['function'] for e in context_events)),
        'limits':[e['seq'] for e in events if e['kind']=='combat_context_limit'],
        'pairs':len(completed), 'diagnosticMatches':sum(c['match'] for c in completed),
        'hiddenEstimates':sum(c['hiddenEstimate'] for c in completed),
        'independentAttributeComputations':0,
        'strictSemanticComplete':0,
        'strictSemanticIncomplete':len(completed),
        'semanticLimitations':['player flags sampled before native consumption',
                               'native attribute and effect outputs are supplied inputs',
                               'JSONL fingerprint attestation must be checked separately'],
        'pathDecisions':dict(Counter(reason for c in completed for reason in c['pathDecisions'])),
        'cases':completed,
    }


def audit_file(path, expected_sha256=None):
    raw = Path(path).read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    require(expected_sha256 is None or digest == expected_sha256.lower(), 'Capture SHA256 mismatch')
    records = [json.loads(line) for line in raw.decode('utf-8-sig').splitlines() if line.strip()]
    return {'captureSha256':digest, **audit(records)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', type=Path)
    parser.add_argument('--expected-sha256')
    args = parser.parse_args()
    try:
        print(json.dumps(audit_file(args.capture,args.expected_sha256),indent=2))
    except (ValueError,KeyError,TypeError,StopIteration,struct.error) as error:
        parser.error(str(error))


if __name__ == '__main__':
    main()
