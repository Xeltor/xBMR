#!/usr/bin/env python3
"""Validate upstream-based release labels and four-part Dalamud versions."""

import argparse
import json
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def release_info(upstream_version, fork_revision, upstream_commit):
    if not isinstance(upstream_version, str) or not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", upstream_version):
        raise ValueError("upstream_version must have four numeric parts")
    parts = tuple(map(int, upstream_version.split(".")))
    if ".".join(map(str, parts)) != upstream_version:
        raise ValueError("upstream_version must not contain leading zeroes")
    if type(fork_revision) is not int or not 0 <= fork_revision < 100:
        raise ValueError("fork_revision must be an integer from 0 to 99")
    if not isinstance(upstream_commit, str) or not re.fullmatch(r"[0-9a-f]{40}", upstream_commit):
        raise ValueError("upstream_commit must be a full lowercase commit SHA")
    encoded = (*parts[:3], parts[3] * 100 + fork_revision)
    if any(part > 65534 for part in encoded):
        raise ValueError("encoded version exceeds the .NET assembly version range")
    version = f"{upstream_version}.{fork_revision}"
    return {
        "source_tag": f"v{version}",
        "release_version": version,
        "assembly_version": ".".join(map(str, encoded)),
        "upstream_version": upstream_version,
        "upstream_commit": upstream_commit,
        "fork_revision": str(fork_revision),
    }


def load_release(root):
    metadata = json.loads((root / "fork-version.json").read_text())
    info = release_info(metadata["upstream_version"], metadata["fork_revision"], metadata["upstream_commit"])
    project = ET.parse(root / "BossMod/BossModReborn.csproj")
    for property_name, expected in (("Version", info["assembly_version"]),
                                    ("InformationalVersion", info["release_version"])):
        values = [node.text for node in project.findall(f"./PropertyGroup/{property_name}")]
        if values != [expected]:
            raise ValueError(f"project {property_name} must be {expected}")
    return info


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-tag")
    args = parser.parse_args()
    info = load_release(Path(__file__).resolve().parents[1])
    if args.expected_tag and args.expected_tag != info["source_tag"]:
        raise SystemExit(f"Source tag must be {info['source_tag']}")
    output_path = os.environ.get("FORGEJO_OUTPUT") or os.environ.get("GITHUB_OUTPUT")
    if output_path:
        with Path(output_path).open("a") as output:
            for key, value in info.items():
                output.write(f"{key}={value}\n")
    print(json.dumps(info, indent=2))


if __name__ == "__main__":
    main()
