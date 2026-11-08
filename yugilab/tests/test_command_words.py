"""Prove replay high-word categories are callsite-limited, not global masking."""
import unittest

from yugilab.command_words import classify_high_word_change as classify


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

    def test_other_callsites_remain_unknown(self):
        for opcode, caller, arg in (
            ("0x0037", "0x58cf96", 0),
            ("0x0086", "0x574804", 1),
        ):
            self.assertEqual(classify(opcode, caller, arg,
                                      0x06350001, 0x06470001),
                             "unresolved_high16")

    def test_never_interprets_low16_differences_as_pointer_residue(self):
        self.assertEqual(classify(
            "0x00CE", "0x5abeab", 1, 0x00EA02E1, 0x00F402E2),
            "low16_changed")
        self.assertEqual(classify(
            "0x00CE", "0x5abeab", 1, 0x00EA02E1, 0x00EA02E1),
            "identical")


if __name__ == "__main__":
    unittest.main()
