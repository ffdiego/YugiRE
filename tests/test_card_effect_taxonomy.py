import unittest
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from card_effect_taxonomy import classify, build, verify_reference

class TaxonomyTests(unittest.TestCase):
    def test_simple(self):
        for description, family in [('Draw 2 cards.', 'draw'), ('Gain 500 Life Points.', 'heal'), ('Inflict 200 points of damage to your opponent.', 'damage')]:
            with self.subTest(description=description):
                self.assertEqual(classify(description, 'Spell'), ([family], 'trivial-candidate'))
        self.assertEqual(classify('Draw two cards from your deck.', 'Spell'),
                         (['draw'], 'trivial-candidate'))

    def test_conservative(self):
        for description, kind, route in [('Draw 2 cards, then discard 1 card.', 'Spell', 'queue-or-response'), ('Draw 2 cards.', 'Trap', 'queue-or-response'), ('Draw 2 cards. Destroy all monsters.', 'Spell', 'review'), ('FLIP: Draw 2 cards.', 'Effect', 'queue-or-response'), ('A dragon that destroys things.', 'Normal', 'not-applicable'), ('', 'Spell', 'review')]:
            with self.subTest(description=description):
                self.assertEqual(classify(description, kind)[1], route)

    def test_pack_and_privacy(self):
        data = dict(fingerprints={'datSha256':'fixture'}, packSha256='fixture', cards=[
            dict(internalIndex=1, resourceId=10, classCode=22, pack=4, description='Draw 2 cards.'),
            dict(internalIndex=2, resourceId=11, classCode=22, pack=0, description='secret'),
            dict(internalIndex=3, resourceId=10, classCode=22, pack=1, description='Draw 2 cards.')])
        result = build(data, {}, {10:'P001'}, {10:'Done'})
        self.assertEqual([r['internalIndex'] for r in result['cards']], [1,3])
        self.assertEqual(result['routes'], {'trivial-candidate':2})
        self.assertEqual(result['cards'][0]['currentStatus'], 'Done')
        self.assertEqual(result['cards'][0]['callbackProfile'], 'P001')
        self.assertNotIn('description', str(result))
        self.assertNotIn('Draw 2 cards.', str(result))
        self.assertNotIn('secret', str(result))
        data['cards'].append(data['cards'][0])
        with self.assertRaises(ValueError):
            build(data, {}, {}, {})

    def test_reference_detects_pack_drift_even_at_same_total(self):
        reference = dict(schemaVersion=1, fields=['resourceId', 'packMask', 'kindCode'],
                         datSha256='fixture', kinds={'1': 'Monster', '2': 'Spell'},
                         cards=[[10, 4, 1], [11, 1, 2]])
        result = dict(fingerprints={'datSha256': 'fixture'}, cards=[
            dict(resourceId=10, packMask=4, kind='Effect'),
            dict(resourceId=11, packMask=1, kind='Spell')])
        verify_reference(result, reference)

        result['cards'][0]['packMask'] = 5
        with self.assertRaisesRegex(ValueError, '1 missing and 1 unexpected'):
            verify_reference(result, reference)

        result['cards'][0]['packMask'] = 4
        result['fingerprints']['datSha256'] = 'different'
        with self.assertRaisesRegex(ValueError, 'DAT fingerprint'):
            verify_reference(result, reference)

if __name__ == '__main__':
    unittest.main()
