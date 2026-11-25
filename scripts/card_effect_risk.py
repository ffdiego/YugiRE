#!/usr/bin/env python3
"""C18: join callback slots, live playability and optionally the private C01 taxonomy.

The public pack index is sufficient for the initial inventory. Semantic risk
uses C01's description-derived families without exporting any description.
This is prioritization, never proof of native behavior or playability.
"""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path
import re

from card_effect_backlog import effect_families, validate_taxonomy
from card_effect_taxonomy import ROOT, verify_reference
from card_playability import CATALOG, MANIFEST, SCOPES, STATUSES, read_manifest, read_subtypes

SLOTS = ('primary', 'candidate', 'availability', 'preparation', 'target_preparation')
RISK = ('low', 'medium', 'high')
FINGERPRINTS = {'cardIdSha256': 'card_id_sha256', 'namesSha256': 'names_sha256',
                'propertiesSha256': 'properties_sha256'}
UNFINISHED = ('Pending', 'Partial')
STATIC_DIR = ROOT / 'docs/research/card-static-sweep'
STATIC_STATUSES = ('Mapeado', 'Parcial', 'Pendente')

# Advisory floors for the audited zero/absent-slot families.
STATIC_MEDIUM = {
    'stats-por-contagem', 'stats-proprios-sem-callback',
    'retorno-e-posicao-de-fase', 'lp-na-standby',
    'uso-por-batalha', 'modificador-global-de-combate',
}
STATIC_HIGH = {
    'lp-por-evento-global', 'custo-e-risco-de-ataque',
    'destinos-de-invocacao-restrita', 'invocacao-por-condicao',
    'regras-de-zona-e-visibilidade', 'protecao-de-umi',
    'condicoes-especificas-de-batalha', 'equipar-destruido',
    'fronteiras-do-dano-de-batalha', 'vinculos-e-protecao-globais',
    'marcadores-apos-ataque', 'toons-classicos', 'vitoria-central-exodia',
}


def callback_slots(catalog):
    """Profiles encode pointer tuples; records encode exact resource IDs."""
    if tuple(catalog['slot_order']) != SLOTS:
        raise ValueError('Unexpected callback slot order')
    profiles = {}
    assigned = {}
    for profile in catalog['profiles']:
        pid, callbacks, rows = (profile[k] for k in ('id', 'callbacks', 'rows'))
        if (not isinstance(pid, str) or pid in profiles or len(callbacks) != len(SLOTS)
                or any(v is not None and (not isinstance(v, str) or
                       not re.fullmatch(r'0x[0-9a-fA-F]+', v) or int(v, 16) == 0)
                       for v in callbacks)):
            raise ValueError('Invalid callback profile')
        profiles[pid] = [slot for slot, ptr in zip(SLOTS, callbacks) if ptr is not None]
        for row in rows:
            if type(row) is not int or row < 0 or row in assigned:
                raise ValueError('Invalid or duplicate profile row')
            assigned[row] = pid
    result = {}
    seen_rows = set()
    for record in catalog['records']:
        rid, row, pid = (record[k] for k in ('resource_id', 'row', 'profile'))
        if (type(rid) is not int or not 0 <= rid < 65535 or rid in result or
                type(row) is not int or row in seen_rows or assigned.get(row) != pid
                or pid not in profiles):
            raise ValueError('Invalid callback record identity/profile')
        result[rid] = (pid, profiles[pid])
        seen_rows.add(row)
    if seen_rows != set(assigned) or len(result) != catalog['table']['count']:
        raise ValueError('Callback row totals differ')
    return result


def static_index(sources):
    """Index existing studies without copying descriptions or conclusions."""
    indexed = {}
    for source, records in sources:
        families = {}
        for entry in records:
            family = entry['family']
            if entry['kind'] == 'family':
                if family in families:
                    raise ValueError('Duplicate static family in a study')
                families[family] = bool(entry.get('pending'))
            elif entry['kind'] == 'card':
                rid, status = entry['resource_id'], entry['status']
                if (type(rid) is not int or not 0 <= rid < 65535 or
                        rid in indexed or family not in families or
                        status not in STATIC_STATUSES):
                    raise ValueError('Invalid or duplicate static card identity')
                indexed[rid] = dict(source=source, family=family, status=status,
                                    familyGap=families[family],
                                    cardGap=bool(entry.get('pending_card')))
            else:
                raise ValueError('Unsupported static record kind')
    return indexed


def read_static_index(directory=STATIC_DIR):
    paths = sorted(directory.glob('[0-9][0-9]-*.jsonl'))
    if not paths:
        raise ValueError('No static evidence studies found')
    return static_index((path.relative_to(ROOT).as_posix(),
                         (json.loads(line) for line in path.read_text(encoding='utf-8').splitlines() if line.strip()))
                        for path in paths)


def apply_static_floor(adjusted, reasons, review, evidence, count):
    if evidence is None or adjusted == 'not-applicable':
        return adjusted, reasons, review
    family = evidence['family']
    if count in (None, 0):
        floor = 'high' if family in STATIC_HIGH else 'medium' if family in STATIC_MEDIUM else None
        if floor:
            adjusted = floor if adjusted == 'indeterminate' else RISK[max(RISK.index(adjusted), RISK.index(floor))]
            reasons = [*reasons, 'static-family-risk-floor']
    if evidence['status'] != 'Mapeado' or evidence['cardGap'] or evidence['familyGap']:
        review = True
        reasons = [*reasons, 'existing-static-evidence-has-gaps']
    return adjusted, reasons, review


def initial_risk(count):
    if count is None or count == 0:
        return 'indeterminate'
    return 'low' if count == 1 else 'medium' if count == 2 else 'high'


def adjust_risk(initial, kind, route, families):
    """Conservative semantic floors, not an automatic ruling parser."""
    if kind == 'Normal':
        return 'not-applicable', ['normal-monster'], False
    if route is None:
        return initial, ['local-taxonomy-unavailable'], True

    reasons = []
    floor = None
    if set(families) & {'negation', 'control', 'restriction'}:
        floor = 'high'
        reasons.append('negation-control-or-restriction')
    elif kind in ('Trap', 'Ritual', 'Fusion') or set(families) & {
            'search', 'flip', 'equip', 'graveyard', 'summon', 'random', 'banish',
            'return', 'position', 'discard', 'destroy', 'stat-change'}:
        floor = 'medium'
        reasons.append('selection-trigger-or-state-dependent-mechanic')
    if route == 'review':
        reasons.append('review-unclassified-or-ambiguous-text')
    if initial == 'indeterminate':
        reasons.append('missing-or-empty-callbacks')
    if route == 'trivial-candidate':
        reasons.append('strict-c01-simple-effect')
    if not reasons:
        reasons.append('slot-count-only')

    # Unknown is not low. A positive semantic floor can raise unknown;
    # an explicit simple C01 match can guide triage even without slots.
    if initial == 'indeterminate':
        adjusted = floor or ('low' if route == 'trivial-candidate' else initial)
    elif floor is None:
        adjusted = initial
    else:
        adjusted = RISK[max(RISK.index(initial), RISK.index(floor))]
    return adjusted, reasons, (initial == 'indeterminate' or adjusted == 'high' or
                               route == 'review' or not families)


def build(catalog, reference, statuses, subtypes, taxonomy=None, mask=7, static_evidence=None):
    callbacks = callback_slots(catalog)
    if (reference['schemaVersion'] != 1 or
            reference['fields'] != ['resourceId', 'packMask', 'kindCode']):
        raise ValueError('Unsupported pack reference format')
    if taxonomy is not None:
        validate_taxonomy(taxonomy)
        verify_reference(taxonomy, reference)
        for key, native in FINGERPRINTS.items():
            if taxonomy['fingerprints'][key] != catalog[native]:
                raise ValueError('C01 and callback catalog fingerprints differ')
        source = taxonomy['cards']
    else:
        source = [dict(resourceId=rid, packMask=pack, kind=(
            subtypes.get(rid, 'EffectOrUnknown') if reference['kinds'][str(code)] == 'Monster'
            else reference['kinds'][str(code)]), families=[], route=None)
                  for rid, pack, code in reference['cards']]
    static_evidence = {} if static_evidence is None else static_evidence
    rows = []
    for entry in source:
        if not entry['packMask'] & mask:
            continue
        rid = entry['resourceId']
        status = statuses.get(rid, 'Pending')
        if status not in STATUSES:
            raise ValueError('Invalid playability status')
        profile, slots = callbacks.get(rid, (None, None))
        count = None if slots is None else len(slots)
        initial = initial_risk(count)
        route = entry['route']
        families = effect_families(route, entry['families'])
        adjusted, reasons, review = adjust_risk(initial, entry['kind'], route, families)
        evidence = static_evidence.get(rid)
        adjusted, reasons, review = apply_static_floor(adjusted, reasons, review, evidence, count)
        row = dict(resourceId=rid, packMask=entry['packMask'], kind=entry['kind'],
                   status=status, callbackProfile=profile, callbackSlots=slots,
                   slotCount=count, initialRisk=initial, adjustedRisk=adjusted,
                   families=families, route=route, reasons=reasons, needsReview=review,
                   staticEvidence=evidence)
        if 'internalIndex' in entry:
            row['internalIndex'] = entry['internalIndex']
        rows.append(row)
    rows.sort(key=lambda r: (r['resourceId'], r.get('internalIndex', -1), r['packMask']))
    # Counts are pack entries, not callback records or normalized card names.
    raw_counts = Counter(str(len(slots)) for _, slots in callbacks.values())
    counts = defaultdict(Counter)
    review_ids = []
    backlog = defaultdict(Counter)
    static_counts = Counter()
    static_families = defaultdict(Counter)
    for row in rows:
        key = 'absent' if row['slotCount'] is None else str(row['slotCount'])
        counts[key][row['status']] += 1
        if row['status'] in UNFINISHED and row['adjustedRisk'] != 'not-applicable':
            backlog[row['adjustedRisk']][row['status']] += 1
            evidence = row['staticEvidence']
            static_counts[evidence['status'] if evidence else 'not-indexed'] += 1
            if evidence:
                static_families[evidence['source'] + ':' + evidence['family']][row['status']] += 1
            if row['needsReview']:
                review_ids.append(row['resourceId'])
    return dict(schemaVersion=1, catalogSha256=catalog['target_sha256'],
                datSha256=reference['datSha256'], semanticSource='C01' if taxonomy else 'none',
                packMask=mask, catalogSlotCounts=dict(sorted(raw_counts.items())),
                packSlotStatusCounts={k: dict(v) for k, v in sorted(counts.items())},
                unfinishedByRisk={k: dict(v) for k, v in sorted(backlog.items())},
                unfinishedStaticCounts=dict(static_counts),
                unfinishedStaticFamilies={k: dict(v) for k, v in sorted(static_families.items())},
                reviewResourceIds=sorted(set(review_ids)), cards=rows)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--taxonomy', type=Path, help='Optional local scratch/card-taxonomy.json')
    p.add_argument('--pack', choices=SCOPES, default='all-packs')
    p.add_argument('--output', type=Path, default=ROOT / 'scratch/card-effect-risk.json')
    args = p.parse_args()
    def read(path):
        return json.loads(path.read_text(encoding='utf-8'))
    try:
        manifest = read_manifest(MANIFEST)
        result = build(read(CATALOG), read(ROOT / 'docs/cards/pack-index.json'),
                       {rid: item['status'] for rid, item in manifest.items()},
                       read_subtypes(), read(args.taxonomy) if args.taxonomy else None,
                       SCOPES[args.pack], read_static_index())
    except (OSError, ValueError, KeyError, TypeError):
        p.error('Cannot read compatible callback, pack, manifest and optional C01 metadata')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + '\n', encoding='utf-8')
    print('Callback records by filled slots:', result['catalogSlotCounts'])
    print('Pack entries by slots and current status:', result['packSlotStatusCounts'])
    print('Unfinished by advisory risk:', result['unfinishedByRisk'])
    print('Unfinished by existing static study:', result['unfinishedStaticCounts'])
    print('Unfinished resource IDs for review:', len(result['reviewResourceIds']))
    if not args.taxonomy:
        print('No C01 taxonomy: C01 semantics unavailable; static family floors are advisory; use --taxonomy to enrich.')


if __name__ == '__main__':
    main()
