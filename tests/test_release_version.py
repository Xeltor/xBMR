import importlib.util
import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("release_version", ROOT / "scripts/release-version.py")
VERSION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERSION)
SHA = "a" * 40


class ReleaseVersionTests(unittest.TestCase):
    def info(self, upstream, revision):
        return VERSION.release_info(upstream, revision, SHA)

    def test_label_and_internal_version(self):
        info = self.info("7.5.6.35", 1)
        self.assertEqual(info["source_tag"], "v7.5.6.35.1")
        self.assertEqual(info["assembly_version"], "7.5.6.3501")

    def test_update_order_across_upstream_reset_and_rollovers(self):
        versions = [("7.5.6.35", 0), ("7.5.6.35", 1), ("7.5.6.35", 99),
                    ("7.5.6.36", 0), ("7.5.7.0", 0), ("7.6.0.0", 0), ("8.0.0.0", 0)]
        encoded = [tuple(map(int, self.info(base, revision)["assembly_version"].split(".")))
                   for base, revision in versions]
        self.assertEqual(encoded, sorted(set(encoded)))

    def test_invalid_versions_are_refused(self):
        for base, revision in [("7.5.6", 1), ("7.5.6.35.1", 1), ("07.5.6.35", 1),
                               ("7.5.6.35", -1), ("7.5.6.35", 100), ("7.5.6.35", True),
                               ("7.5.6.35", 1.0), ("7.5.6.656", 0), ("65535.0.0.0", 0)]:
            with self.subTest(base=base, revision=revision), self.assertRaises(ValueError):
                self.info(base, revision)

    def test_invalid_upstream_commit_is_refused(self):
        with self.assertRaises(ValueError):
            VERSION.release_info("7.5.6.35", 1, "not-a-sha")

    def test_project_and_metadata_agree(self):
        metadata = json.loads((ROOT / "fork-version.json").read_text())
        self.assertEqual(VERSION.load_release(ROOT)["source_tag"],
                         f"v{metadata['upstream_version']}.{metadata['fork_revision']}")


if __name__ == "__main__":
    unittest.main()
