#!/usr/bin/env python3
"""Summarize unfinished effect families using the local C01 taxonomy and current manifest."""

import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path
import re

from card_effect_taxonomy import FAMILIES, MONSTER_SUBTYPES, ROOT, SIMPLE, verify_reference
from card_playability import MANIFEST, SCOPES, STATUSES, read_manifest

ROUTES = ('trivial-candidate', 'review', 'queue-or-response')
UNFINISHED = ('Pending', 'Partial')
ROW_FIELDS = {'internalIndex', 'resourceId', 'packMask', 'kind', 'families', 'route',
              'callbackProfile', 'currentStatus'}
FINGERPRINT_FIELDS = {'datSha256', 'cardIdSha256', 'namesSha256', 'propertiesSha256'}


def validate_taxonomy(taxonomy):
    fields = {'schemaVersion', 'fingerprints', 'packSha256', 'routes', 'cards'}
    if (not isinstance(taxonomy, dict) or taxonomy.keys() != fields or
            type(taxonomy['schemaVersion']) is not int or taxonomy['schemaVersion'] != 1 or
            not isinstance(taxonomy['cards'], list) or not taxonomy['cards'] or
            not isinstance(taxonomy['fingerprints'], dict) or
            taxonomy['fingerprints'].keys() != FINGERPRINT_FIELDS):
        raise ValueError('Unsupported C01 taxonomy schema')
    hashes = [*taxonomy['fingerprints'].values(), taxonomy['packSha256']]
    if any(not isinstance(digest, str) or re.fullmatch(r'[0-9a-f]{64}', digest) is None for digest in hashes):
        raise ValueError('Invalid C01 taxonomy fingerprint format')

    route_counts = Counter()
    indices = set()
    allowed_routes = (*ROUTES, 'not-applicable')
    allowed_kinds = MONSTER_SUBTYPES | {'Spell', 'Trap', 'Other'}
    for row in taxonomy['cards']:
        if not isinstance(row, dict) or row.keys() != ROW_FIELDS:
            raise ValueError('Unsupported C01 taxonomy record shape')
        index, resource, mask = row['internalIndex'], row['resourceId'], row['packMask']
        if (type(index) is not int or index not in range(4096) or index in indices or
                type(resource) is not int or resource not in range(65535) or
                type(mask) is not int or mask not in range(1, 8)):
            raise ValueError('Invalid or duplicate C01 taxonomy record identity')
        indices.add(index)

        kind, route, status, families = row['kind'], row['route'], row['currentStatus'], row['families']
        if row['callbackProfile'] is not None and not isinstance(row['callbackProfile'], str):
            raise ValueError('Invalid C01 taxonomy callback profile')
        if (not isinstance(kind, str) or kind not in allowed_kinds or route not in allowed_routes or
                status not in STATUSES or not isinstance(families, list) or
                any(not isinstance(family, str) or family not in FAMILIES for family in families) or
                len(families) != len(set(families))):
            raise ValueError('Invalid C01 taxonomy kind, route, status or families')
        if ((kind == 'Normal') != (route == 'not-applicable') or
                (route == 'not-applicable' and families) or
                (route == 'trivial-candidate' and
                 (kind != 'Spell' or len(families) != 1 or families[0] not in SIMPLE))):
            raise ValueError('Inconsistent C01 taxonomy route and families')
        route_counts[route] += 1

    declared = taxonomy['routes']
    if (not isinstance(declared, dict) or any(type(count) is not int for count in declared.values()) or
            declared != dict(route_counts)):
        raise ValueError('C01 taxonomy route totals differ from the records')


def summarize(taxonomy, statuses, mask):
    validate_taxonomy(taxonomy)
    totals = {route: Counter() for route in ROUTES}
    by_family = {route: defaultdict(Counter) for route in ROUTES}
    updated = 0
    for row in taxonomy['cards']:
        if not row['packMask'] & mask:
            continue
        status = statuses.get(row['resourceId'], 'Pending')
        if status not in STATUSES:
            raise ValueError('Invalid playability status')
        updated += status != row['currentStatus']
        route = row['route']
        if route not in totals or status not in UNFINISHED:
            continue
        totals[route][status] += 1
        for family in row['families'] or ['unclassified']:
            by_family[route][family][status] += 1
    return totals, by_family, updated


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('taxonomy', type=Path, help='Local scratch/card-taxonomy.json from C01')
    parser.add_argument('--pack', choices=SCOPES, default='all-packs')
    args = parser.parse_args()

    try:
        taxonomy = json.loads(args.taxonomy.read_text(encoding='utf-8'))
        manifest = read_manifest(MANIFEST)
        statuses = {resource: record['status'] for resource, record in manifest.items()}
        totals, by_family, updated = summarize(taxonomy, statuses, SCOPES[args.pack])
        reference = json.loads((ROOT / 'docs/cards/pack-index.json').read_text(encoding='utf-8'))
        verify_reference(taxonomy, reference)
    except (OSError, ValueError, KeyError):
        parser.error('Cannot read valid C01 taxonomy and playability metadata for the pinned DAT')

    print(f'Effect backlog for {args.pack} using the current manifest')
    print(f'Statuses updated since the taxonomy snapshot: {updated}')
    print('Each family count includes every matching entry. Route totals count entries once.')
    for route in ROUTES:
        total = totals[route]
        print(f'\n{route}: {total["Pending"]} Pending / {total["Partial"]} Partial')
        ranked = sorted(by_family[route].items(), key=lambda entry: (-entry[1].total(), entry[0]))
        for family, counts in ranked:
            print(f'  {family}: {counts["Pending"]} Pending / {counts["Partial"]} Partial')


if __name__ == '__main__':
    main()
