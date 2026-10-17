"""Check real process exit status: a failed check must exit 1, never abort."""
import os
from pathlib import Path
import shutil
import subprocess
import unittest


class ConsoleRunnerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[1]
        portable = Path.home() / ".local/share/xbmr-dotnet/dotnet"
        cls.dotnet = os.environ.get("XBMR_DOTNET") or (str(portable) if portable.is_file() else shutil.which("dotnet"))
        if not cls.dotnet:
            raise unittest.SkipTest("A compatible .NET SDK is required (set XBMR_DOTNET)")
        cls.project = cls.root / "tests/ConsoleRunnerRegression/ConsoleRunnerRegression.csproj"
        cls.build_flags = ["-p:UseSharedCompilation=false", "-m:1", "-nr:false"]
        result = subprocess.run([cls.dotnet, "build", str(cls.project), "-c", "Release", *cls.build_flags],
                                cwd=cls.root, capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        cls.dll = cls.project.parent / "bin/Release/net10.0/ConsoleRunnerRegression.dll"

    def run_fixture(self, mode):
        return subprocess.run([self.dotnet, str(self.dll), mode], cwd=self.root,
                              capture_output=True, text=True)

    def test_success(self):
        result = self.run_fixture("success")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.strip(), "checks passed")
        self.assertEqual(result.stderr, "")

    def test_failures_exit_without_signal(self):
        for mode, diagnostic in [("assertion", "regression assertion failed"),
                                 ("bootstrap", "FileNotFoundException"),
                                 ("reflection", "854246")]:
            with self.subTest(mode=mode):
                result = self.run_fixture(mode)
                self.assertEqual(result.returncode, 1, result.stderr)
                self.assertEqual(result.stdout, "")
                self.assertIn(diagnostic, result.stderr)
                self.assertNotIn("Unhandled exception", result.stderr)
                self.assertNotIn("TargetInvocationException", result.stderr)
                if mode == "reflection":
                    self.assertIn("Fixture.Inner()", result.stderr)



if __name__ == "__main__":
    unittest.main()
