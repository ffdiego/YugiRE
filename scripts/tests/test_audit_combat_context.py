"""Synthetic instrumentation contracts, not native equivalence tests."""
import copy
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from audit_combat_context import audit_file, diagnostic, half


def case(attack=1700, defense=False):
    import struct
    entry = {'argsRaw':[0,0,0], 'seedHex':'00000000',
             'attacker':{'internalCardIndex':24,'playerFlagsRaw':0},
             'defender':{'internalCardIndex':550,'playerFlagsRaw':0,
                         'faceUp':True,'defensePosition':defense}}
    own = struct.pack('<HBBii',24,15,5,attack,1150)
    enemy = struct.pack('<HBBii',550,18,5,1900,900)
    children = []

    def add(name,caller,args,raw=None,output=None):
        event = {'function':name, 'caller':hex(caller), 'argsRaw':args,
                 'seq':len(children)+1}
        if raw is not None:
            event.update(returnRaw=raw,returnSigned=raw,resourceId=raw & 0xFFFF)
        if output is not None:
            event['outputHex'] = output.hex()
        children.append(event)
    add('own_stats',0x5B0F97,[0,0,123],output=own)
    add('opponent_stats',0x5B0FAE,[1,0,456],output=enemy)
    add('resource',0x5B0FB8,[int.from_bytes(own[:4],'little')],77)
    add('resource',0x5B1009,[int.from_bytes(enemy[:4],'little')],531)
    add('equipment_count',0x5B1060,[0,0,0x291],0)
    if defense:
        add('resource',0x5B10FD,[int.from_bytes(own[:4],'little')],77)
        for caller,resource,side in ((0x5B1117,0x521,0),(0x5B112F,0x604,0),
                                     (0x5B1153,0x521,1),(0x5B116F,0x604,1)):
            add('context_count',caller,[0,0,resource,side],0)
    else:
        add('field_query',0x5B1227,[0,0x58F],0)
    add('resource',0x5B1251,[int.from_bytes(own[:4],'little')],77)
    return entry,children


class CombatContextAuditTests(unittest.TestCase):
    def test_diagnostic_reads_inputs_without_expected_output(self):
        output,decisions,_ = diagnostic(*case())
        self.assertEqual(output,'000438FF')
        self.assertIn('loss:field-nonpositive;phase-not-evaluated',decisions)

    def test_missing_required_predicate_rejected(self):
        entry,children = case()
        del children[-2]
        with self.assertRaisesRegex(ValueError,'Unexpected call'):
            diagnostic(entry,children)

    def test_true_field_requires_phase_not_default_false(self):
        entry,children = case()
        children[-2].update(returnRaw=1,returnSigned=1)
        with self.assertRaisesRegex(ValueError,'phase_query'):
            diagnostic(entry,children)

    def test_wrong_side_rejected(self):
        entry,children = case()
        children[-2]['argsRaw'][0] = 1
        with self.assertRaisesRegex(ValueError,'Arguments'):
            diagnostic(entry,children)

    def test_unused_predicate_not_accepted_as_observed(self):
        entry,children = case()
        children.insert(-1,dict(children[-2],function='phase_query',caller='0x5b1238',argsRaw=[0x14D]))
        with self.assertRaisesRegex(ValueError,'Unexpected call'):
            diagnostic(entry,children)

    def test_defense_branch_consumes_four_context_calls(self):
        self.assertEqual(diagnostic(*case(defense=True))[0],'08080000')

    def test_short_circuit_does_not_require_second_count(self):
        entry,children = case(defense=True)
        children[6].update(returnRaw=1,returnSigned=1)  # 521 -> gain
        del children[7]  # 604 skipped
        children.insert(-1,{'function':'field_query','caller':'0x5b11f4',
                           'argsRaw':[1,0x58F],'returnRaw':0,'returnSigned':0,'seq':50})
        output,decisions,_ = diagnostic(entry,children)
        self.assertEqual(output,'08082003')
        self.assertIn('pierce-own:521-true;604-not-evaluated',decisions)

    def test_resource_uses_low16_of_return_and_full_header_input(self):
        entry,children = case()
        children[2]['returnRaw'] |= 0xDD0000
        children[2]['returnSigned'] = children[2]['returnRaw']
        self.assertEqual(diagnostic(entry,children)[0],'000438FF')

    def test_half_signed_wrap(self):
        self.assertEqual(half(-3),-1)
        self.assertEqual(half(0x7FFFFFFF),-214748364)

    def test_original_capture_when_available(self):
        path = Path(__file__).resolve().parents[2] / 'scratch/captures/commands-20261112-015214-332209-pid10848.jsonl'
        if not path.is_file():
            self.skipTest('Private original capture unavailable')
        report = audit_file(path,'424cb09a6d990dbfb0e8795cf97d0260f644f0c2349980937e0b96d14831213f')
        self.assertEqual((report['pairs'],report['diagnosticMatches']),(64,64))
        self.assertFalse(report['exeAttestedInJsonl'])
        self.assertEqual(report['hiddenEstimates'],57)
        self.assertEqual(report['limits'],[971])
        records = [json.loads(line) for line in path.read_text().splitlines()]
        altered = copy.deepcopy(records)
        victim = next(e for e in altered if e.get('function')=='equipment_count')
        altered.remove(victim)
        from audit_combat_context import audit
        with self.assertRaises(ValueError):
            audit(altered)
