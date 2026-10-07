"""The Android runtime must exercise the exact same wire corpus as C#/JVM CI."""
from pathlib import Path
import unittest

class DebtWireFixtureTests(unittest.TestCase):
    def test_android_and_parity_corpus_match(self):
        root=Path(__file__).resolve().parents[1]
        self.assertEqual((root/'tests/debt/wire-fixtures.json').read_bytes(),
                         (root/'app/src/androidTest/assets/debt-wire-fixtures.json').read_bytes())
