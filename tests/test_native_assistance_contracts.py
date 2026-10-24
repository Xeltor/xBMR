"""The cutover retires only the integration surface added for standalone XCAI."""
import json
from pathlib import Path
import re
import unittest


class NativeAssistanceContracts(unittest.TestCase):
    def test_registration_provenance(self):
        root = Path(__file__).resolve().parents[1]
        baseline = json.loads((root / "tests/NativeMovementRegression/Baseline.json").read_text())
        source = (root / "BossMod/Framework/IPCProvider.cs").read_text()
        actual = re.findall(r'Register\("([^"]+)"', source)
        # The pinned upstream registers AI.GetPreset and AI.SetPreset twice.
        # This test checks contract names; cutover does not refactor upstream IPC.
        self.assertEqual(set(actual), set(baseline["preservedUpstreamEndpoints"]))
        self.assertTrue(set(actual).isdisjoint(baseline["retiringIntegrationEndpoints"]))
        self.assertEqual(len(baseline["retiringIntegrationEndpoints"]), 72)

    def test_native_composition_has_no_standalone_transport(self):
        root = Path(__file__).resolve().parents[1]
        source = (root / "BossMod/Framework/Plugin.cs").read_text()
        self.assertNotIn("IntegrationCoordinator", source)
        for path in (root / "BossMod/CombatAssistance").rglob("*.cs"):
            self.assertNotRegex(path.read_text(), r'"BossMod\.[^"]*\.V1\.', str(path))


if __name__ == "__main__":
    unittest.main()
