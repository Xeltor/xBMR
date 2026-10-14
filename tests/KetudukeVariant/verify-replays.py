"""Check private CandidateRunner reports against retained numeric event inputs.

Usage: python tests/KetudukeVariant/verify-replays.py /path/to/private/reports
The directory contains all-evidence.json and N-M-{request,report}.json. Original
recordings and private reports stay outside Git. These checks do not measure
physical boundaries, server snapshots, continuous paths, or live controller use.
"""

import json
import math
import sys
from collections import Counter
from pathlib import Path

base = Path(sys.argv[1])
records = json.loads((base / "all-evidence.json").read_text())
manifest = json.loads((base / "source-manifest.json").read_text())
counts = Counter()
navigation = []
origin_residuals = []
for index, record in enumerate(records, 1):
    events = {event["seq"]: event for event in record["events"]}
    sources = list(base.glob(f"{index}-*-report.json"))
    assert sources, "missing source reports"
    samples = {}
    for path in sources:
        report = json.loads(path.read_text())
        assert report["ok"] and report["module_errors"] == 0
        assert not report["missing_samples"] and not report["raw_operations_not_applied"]
        assert not report["unsupported_outputs"]
        assert report["source_coverage"]["source_digest"] == record["recording"]["source_digest"]
        assert report["candidate"]["assembly_sha256"] == manifest["assembly_sha256"]
        assert report["candidate"]["source_sha256"] == manifest["source_sha256"]
        assert any(event["kind"] == "activated" and event["component_count_at_callback"] == 10 for event in report["lifecycle"])
        assert any(event["kind"] == "deactivated" and event["component_count_at_callback"] == 0 for event in report["lifecycle"])
        counts["reports"] += 1
        for sample in report["samples"]:
            assert sample["available"], sample["id"]
            assert sample["id"] not in samples
            samples[sample["id"]] = sample
    counts["original_sources"] += 1
    counts["native_samples"] += len(samples)
    for key, sample in samples.items():
        seq, boundary = key.split("-", 1)
        event = events[int(seq)]
        if boundary != "after_seq":
            continue
        before = samples[f"{seq}-before_seq"]
        aid = event["action"]
        if event["kind"] == "cast_start" and aid in (35452, 35453, 35454, 35455):
            aoe = next(aoe for aoe in sample["aoes"] if aoe["component"] == "SpringCrystals" and aoe["authored_source_actor"] == event["actor"])
            origin = event["position"]
            assert math.hypot(aoe["origin"]["x"] - origin["x"], aoe["origin"]["z"] - origin["z"]) < 0.0001
            assert abs(aoe["rotation"] - origin["facing"]) < 0.00001
            expected = event["ticks"] + round((event["duration"] + 0.3) * 10000000)
            assert abs(aoe["activation_ticks"] - expected) < 10000, "native cast-timer mismatch"
            matching = [constraint for constraint in sample["ai_forbidden_constraints"] if constraint["activation_ticks"] == aoe["activation_ticks"]]
            assert any({probe["id"]: probe["forbidden"] for probe in constraint["probes"]}.get("inside") is True
                       and {probe["id"]: probe["forbidden"] for probe in constraint["probes"]}.get("outside") is False for constraint in matching)
            counts["crystal_cast_origin_and_deadline_checks"] += 1
            counts["nominal_boundary_probes"] += 2
            earlier = next(aoe for aoe in before["aoes"] if aoe["component"] == "SpringCrystals" and aoe["authored_source_actor"] == event["actor"])
            cx, cz = origin["x"], origin["z"]
            if aid in (35454, 35455):
                cx += 38 * math.sin(origin["facing"])
                cz += 38 * math.cos(origin["facing"])
            residual = math.hypot(earlier["origin"]["x"] - cx, earlier["origin"]["z"] - cz)
            origin_residuals.append(residual)
        elif event["kind"] == "action" and aid in (35452, 35453, 35454, 35455):
            def crystal_sources(row):
                return {aoe["authored_source_actor"] for aoe in row["aoes"] if aoe["component"] == "SpringCrystals"}
            old = crystal_sources(before)
            new = crystal_sources(sample)
            assert event["actor"] in old and new == old - {event["actor"]}, "crystal resolution affected another instance"
            counts["individual_crystal_resolution_checks"] += 1
        elif event["kind"] == "action" and aid == 35463:
            old = [aoe for aoe in before["aoes"] if aoe["component"] == "StrewnBubbles"]
            new = [aoe for aoe in sample["aoes"] if aoe["component"] == "StrewnBubbles"]
            assert len(new) == len(old) - 1, "helper did not retire exactly one lane"
            counts["individual_lane_resolution_checks"] += 1
        elif event["kind"] == "action" and aid in (36125, 36126):
            assert sample["ai_damage_predictions"]["count"] == before["ai_damage_predictions"]["count"] - 1
            counts["water_iii_helper_resolution_checks"] += 1
        elif event["kind"] == "action" and aid == 36116:
            assert sample["ai_damage_predictions"] == before["ai_damage_predictions"], "visual must not retire delayed Water III"
            counts["water_iii_visual_preserves_damage_checks"] += 1
        elif event["kind"] == "action" and aid in (35485, 35487, 35488, 35486):
            old = [aoe for aoe in before["aoes"] if aoe["component"] == "Twintides"]
            new = [aoe for aoe in sample["aoes"] if aoe["component"] == "Twintides"]
            assert len(old) == 1 and len(new) == (1 if aid in (35485, 35487) else 0)
            counts["twintides_progression_checks"] += 1
        elif event["kind"] == "action" and aid == 35494:
            assert sample["ai_damage_predictions"]["count"] == before["ai_damage_predictions"]["count"] - 1
            counts["raidwide_helper_resolution_checks"] += 1
        nav = sample["native_navigation_step"]
        if nav.get("requested"):
            assert nav["available"] and nav["map"]["width"] == nav["map"]["height"] == 80
            assert nav["map"]["center"] == {"x": -790, "z": -395}
            if nav["destination"] is not None:
                point = nav["destination"]
                assert abs(point["x"] + 790) < 20 and abs(point["z"] + 395) < 20
                counts["native_destinations_inside_nominal_arena"] += 1
            counts["native_arena_grid_checks"] += 1
            navigation.append({"source": index, "sample": key, "destination": nav["destination"], "casting": nav["native_is_casting"]})
summary = {"counts": dict(counts), "navigation": navigation,
           "early_origin_comparisons": len(origin_residuals),
           "max_early_origin_residual_yalms": max(origin_residuals),
           "early_origins_exceeding_previous_trial_0_03_yalm_tolerance": sum(value > 0.03 for value in origin_residuals),
           "source_sha256": manifest["source_sha256"], "assembly_sha256": manifest["assembly_sha256"],
           "physical_geometry_verified": False, "live_gameplay_verified": False,
           "exact_historical_game_assets_available": False, "pristine_holdout": False}
(base / "validation-summary.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps(summary["counts"]))
