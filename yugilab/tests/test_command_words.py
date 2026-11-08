"""Prove replay high-word categories are callsite-limited, not global masking."""
import unittest

from yugilab.command_words import (
    classify_high_word_change as classify,
    direct_queued_words,
)


class CommandWordOriginTests(unittest.TestCase):
    def test_verified_card_id_lookup_leaves_high_eax_pointer(self):
        for opcode, caller in (
            ("0x00CE", "0x5abeab"),
            ("0x00CF", "0x57977f"),
            ("0x00E6", "0x5abd70"),
            ("0x006F", "0x5abf22"),
            ("0x0072", "0x5783f2"),
            ("0x00DF", "0x595142"),
            ("0x0075", "0x578459"),
            ("0x0072", "0x5783bc"),
            ("0x00E6", "0x5acf72"),
        ):
            with self.subTest(opcode=opcode, caller=caller):
                self.assertEqual(
                    classify(opcode, caller, 1, 0x00EA02E1, 0x00F402E1),
                    "verified_card_id_pointer_residue")

    def test_partial_dx_write_preserves_upper_edx(self):
        self.assertEqual(classify(
            "0x0061", "0x5ba000", 0, 0x05370061, 0x05360061),
            "verified_partial_register_write")

    def test_specific_unrelated_high_words_do_not_inherit_card_id_proof(self):
        self.assertEqual(classify(
            "0x0072", "0x5783bd", 1, 0x00EA02E1, 0x00F402E1),
            "unresolved_high16")
        self.assertEqual(classify(
            "0x00E6", "0x5acf72", 0, 0x00EA02E1, 0x00F402E1),
            "unresolved_high16")

    def test_recovered_37_proves_stale_eax_high_word(self):
        self.assertEqual(classify(
            "0x0037", "0x58cf96", 0, 0x06350037, 0x06470037),
            "verified_partial_register_write")

    def test_86_source_is_not_yet_proven(self):
        self.assertEqual(classify(
            "0x0086", "0x574804", 1, 0x06350445, 0x06470445),
            "unresolved_high16")
        # Do not apply a proven 0x37 classification to unrelated callers.
        self.assertEqual(classify(
            "0x0037", "0x58cf97", 0, 0x06350037, 0x06470037),
            "unresolved_high16")

    def test_native_queue_words_for_37_61_86_ignore_high_register_residue(self):
        vectors = [
            ([0x06350037, 0, 0, 0], [0x06470037, 0, 0, 0],
             (0x0037, 0, 0, 0)),
            ([0x05370061, 5, 1, 0], [0x05360061, 5, 1, 0],
             (0x0061, 5, 1, 0)),
            ([0x0086, 0x06350445, 0x0101, 12],
             [0x0086, 0x06470445, 0x0101, 12],
             (0x0086, 0x0445, 0x0101, 12)),
        ]
        for a, b, expected in vectors:
            with self.subTest(packet=expected):
                self.assertEqual(direct_queued_words(a), expected)
                self.assertEqual(direct_queued_words(b), expected)

    def test_other_producer_opcodes_must_be_decoded_separately(self):
        self.assertIsNone(direct_queued_words([0x00CE, 0xEA02E1, 0, 0]))
        self.assertIsNone(direct_queued_words([0x0064, 5, 0, 0]))
        with self.assertRaises(ValueError):
            direct_queued_words([0x86, 1, 2])
        with self.assertRaises(ValueError):
            direct_queued_words([0x86, -1, 2, 3])

    def test_never_interprets_low16_differences_as_pointer_residue(self):
        self.assertEqual(classify(
            "0x00CE", "0x5abeab", 1, 0x00EA02E1, 0x00F402E2),
            "low16_changed")
        self.assertEqual(classify(
            "0x00CE", "0x5abeab", 1, 0x00EA02E1, 0x00EA02E1),
            "identical")


if __name__ == "__main__":
    unittest.main()
