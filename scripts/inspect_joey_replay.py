#!/usr/bin/env python3
"""Read-only exploratory parser for Joey the Passion YGR/YDR replay files.

Grounded in one observed file. It validates a structural model but does not
claim a complete semantic specification. Standard library only.

Usage:
  python inspect_joey_replay.py replay.ydr
  python inspect_joey_replay.py replay.ydr --csv commands.csv --json summary.json
"""
import argparse
import collections
import csv
import datetime as dt
import json
import struct
from pathlib import Path

# Based on established YugiRE reverse engineering. Labels are NOT a complete
# classification of all commands; 0x8000's meaning also needs native validation.
CANDIDATE_LABELS = {
    0x01: 'inicio_de_turno_provavel',
    0x02: 'limpeza_de_turno',
    0x03: 'transicao_de_turno',
    0x04: 'termino_de_duelo_provavel',
    0x30: 'comparacao_de_batalha',
    0x31: 'apresentacao_de_resultado_de_batalha',
    0x3C: 'snapshot_para_remocao_de_monstro',
    0x43: 'atualizacao_de_lp',
    0x61: 'compra_de_cartas',
    0x7C: 'movimentacao_pos_batalha',
    0xCE: 'colocacao_de_carta',
}


def read_file(path: Path):
    data = path.read_bytes()
    if len(data) < 32 or data[:4] != b'YGR\x00':
        raise ValueError('Invalid or unsupported file signature: expected YGR\\0')

    def u16_at(pos):
        if pos + 2 > len(data):
            raise ValueError(f'Truncated uint16 at 0x{pos:X}')
        return struct.unpack_from('<H', data, pos)[0]

    def u32_at(pos):
        if pos + 4 > len(data):
            raise ValueError(f'Truncated uint32 at 0x{pos:X}')
        return struct.unpack_from('<I', data, pos)[0]

    header = {
        'signature': 'YGR\\0',
        'save_identifier_hex': data[4:12].hex().upper(),
        'header_fields_uninterpreted': {
            f'0x{x:02X}': u32_at(x) for x in (0x0C, 0x10, 0x14, 0x18)
        },
        'field_0x18_possible_unix_datetime_utc': dt.datetime.fromtimestamp(
            u32_at(0x18), tz=dt.timezone.utc).isoformat(),
    }

    cursor = 0x1C
    decks = []
    for side in range(2):
        groups = []
        for group_index in range(3):
            count = u16_at(cursor)
            cursor += 2
            if count > (len(data) - cursor) // 2:
                raise ValueError('Truncated card list')
            group = list(struct.unpack_from(f'<{count}H', data, cursor)) if count else []
            cursor += count * 2
            groups.append({'index': group_index, 'count': count, 'resource_ids': group})
        decks.append({'side_index': side, 'groups': groups})

    count_offset = cursor
    command_count = u32_at(cursor)
    cursor += 4
    if len(data) - cursor != command_count * 8:
        raise ValueError('Command count does not match the remaining file length')

    records = []
    turn = 0
    for i in range(command_count):
        offset = cursor + i * 8
        raw, p1, p2, p3 = struct.unpack_from('<4H', data, offset)
        opcode = raw & 0x7FFF
        high_bit = raw >> 15
        if opcode == 0x01:
            turn += 1
        records.append({
            'record_index': i,
            'offset_hex': f'0x{offset:04X}',
            'turn_number_inferred': turn,
            'raw_opcode_hex': f'0x{raw:04X}',
            'opcode_hex': f'0x{opcode:04X}',
            'high_bit_side_candidate': high_bit,
            'argument_1': p1,
            'argument_2': p2,
            'argument_3': p3,
            'candidate_label': CANDIDATE_LABELS.get(opcode, ''),
        })

    opcode_counts = collections.Counter(rec['opcode_hex'] for rec in records)
    turn_records = [rec for rec in records if rec['opcode_hex'] == '0x0001']
    initial_draws = [rec for rec in records if rec['opcode_hex'] == '0x0061'
                     and rec['argument_2'] == 5]
    summary = {
        'input_file': path.name,
        'bytes': len(data),
        'header': header,
        'deck_groups': decks,
        'command_count_field_offset_hex': f'0x{count_offset:X}',
        'commands_start_offset_hex': f'0x{cursor:X}',
        'record_width_bytes': 8,
        'record_count': len(records),
        'distinct_opcodes': len(opcode_counts),
        'opcode_counts': dict(sorted(opcode_counts.items())),
        'high_bit_counts': dict(sorted(collections.Counter(
            r['high_bit_side_candidate'] for r in records).items())),
        'turn_start_records': len(turn_records),
        'turn_start_high_bits_alternate': all(
            x['high_bit_side_candidate'] != y['high_bit_side_candidate']
            for x, y in zip(turn_records, turn_records[1:])),
        'initial_five_card_draws': len(initial_draws),
        'last_six_records': records[-6:],
        'interpretation_limits': [
            'Names of groups 1 and 2 and header fields are not confirmed.',
            '0x8000 is inferred as a side marker; native callsite verification pending.',
            'Labels are tentative or derived from earlier static RE, not validated by this replay alone.',
            'Replay structure does not prove replay contains all player decisions or randomness.',
        ],
    }
    return records, summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path, help='Replay .ydr path')
    parser.add_argument('--csv', type=Path, help='Save all records to CSV')
    parser.add_argument('--json', type=Path, help='Save header and statistics to JSON')
    args = parser.parse_args()
    records, summary = read_file(args.input)
    if args.csv:
        with args.csv.open('w', encoding='utf-8', newline='') as out:
            writer = csv.DictWriter(out, fieldnames=list(records[0]))
            writer.writeheader()
            writer.writerows(records)
    if args.json:
        args.json.write_text(json.dumps(summary, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print('File:', args.input, '| Bytes:', summary['bytes'])
    print('Groups:', [[g['count'] for g in d['groups']] for d in summary['deck_groups']])
    print('Record count:', summary['record_count'], 'Distinct opcodes:', summary['distinct_opcodes'])
    print('Inferred turn starts:', summary['turn_start_records'],
          '| High bits alternate:', summary['turn_start_high_bits_alternate'])
    print('Initial five-card draws:', summary['initial_five_card_draws'])
    print('Most frequent opcodes:', collections.Counter(summary['opcode_counts']).most_common(10))


if __name__ == '__main__':
    main()
