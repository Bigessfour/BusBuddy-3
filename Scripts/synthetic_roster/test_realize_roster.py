"""Roster realization tests. They do not train CTGAN."""

from __future__ import annotations

import sys
from pathlib import Path

import pandas as pd
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))

import generate_roster as roster
from generate_roster import CORRIDORS, ROSTER_COLUMNS

INTAKE_COLUMNS = [
    "StopNumber",
    "PickupTime",
    "StudentName",
    "Grade",
    "School",
    "SchoolYear",
    "HomeAddress",
    "City",
    "State",
    "Zip",
    "GuardianName",
    "GuardianPhone",
    "HomePhone",
    "EmergencyContactName",
    "EmergencyContactPhone",
    "PickupMode",
    "PickupStopId",
    "AMRoute",
    "PMRoute",
    "RidesAm",
    "RidesPm",
    "RequiresSpecialNeedsBus",
    "RequiresAide",
    "RequiresWheelchair",
    "RequiresSeatBelt",
    "HasMedicalNeeds",
    "Active",
    "TransportationNotes",
]

FLAG_COLUMNS = [
    "RequiresSpecialNeedsBus",
    "RequiresAide",
    "RequiresWheelchair",
    "RequiresSeatBelt",
    "HasMedicalNeeds",
]


def sample_frame(per_corridor: int = 8, seed: int = 2026) -> pd.DataFrame:
    import random

    rng = random.Random(seed)
    rows: list[dict[str, object]] = []
    for corridor in CORRIDORS:
        for index in range(per_corridor + 3):
            if index % 7 == 0 and index % 2 == 0:
                rides_am, rides_pm = "yes", "no"
                north_miles = -2.0
            elif index % 7 == 0:
                rides_am, rides_pm = "no", "yes"
                north_miles = -1.5
            else:
                rides_am, rides_pm = "yes", "yes"
                north_miles = rng.uniform(0, 1)
            rows.append(
                {
                    "corridor": corridor,
                    "grade_band": "elementary",
                    "rides_am": rides_am,
                    "rides_pm": rides_pm,
                    "north_miles": north_miles,
                    "east_miles": rng.uniform(-1, 1),
                }
            )
    return pd.DataFrame(rows)


def corridor_of(row: pd.Series) -> str:
    for column in ("AMRoute", "PMRoute"):
        name = row[column]
        if not isinstance(name, str) or not name:
            continue
        if name.endswith("-PM"):
            return name[: -len("-PM")].removeprefix("Lamar ")
        return name.removeprefix("Lamar ")
    raise AssertionError("row is not on a route")


def test_header_matches_intake_columns() -> None:
    frame = roster.realize_roster(sample_frame(), riders_per_route=4, seed=2026)
    assert list(frame.columns) == INTAKE_COLUMNS
    assert list(frame.columns) == list(ROSTER_COLUMNS)
    assert "Latitude" not in frame.columns
    assert "Longitude" not in frame.columns


def test_five_corridors_routes_and_flags() -> None:
    riders = 8
    frame = roster.realize_roster(sample_frame(), riders_per_route=riders, seed=2026)
    frame = frame.copy()
    frame["Corridor"] = frame.apply(corridor_of, axis=1)
    assert set(frame["Corridor"]) == set(CORRIDORS)
    assert frame.groupby("Corridor").size().eq(riders).all()

    morning = set(frame.loc[frame["RidesAm"] == "true", "AMRoute"])
    afternoon = set(frame.loc[frame["RidesPm"] == "true", "PMRoute"])
    assert morning == {f"Lamar {corridor}" for corridor in CORRIDORS}
    assert afternoon == {f"Lamar {corridor}-PM" for corridor in CORRIDORS}
    for name in morning | afternoon:
        assert "special needs" not in name.casefold()
    assert all(name.endswith("-PM") for name in afternoon)
    assert all(not name.endswith("-PM") for name in morning)

    assert set(frame["PickupMode"]) == {"Home"}
    assert set(frame["PickupStopId"]) == {""}
    assert set(frame["Active"]) == {"true"}
    assert set(frame["Zip"]) == {"81052"}
    assert set(frame["City"]) == {"Lamar"}
    assert set(frame["State"]) == {"CO"}
    for column in FLAG_COLUMNS:
        assert set(frame[column]) == {"false"}

    am_only = frame[(frame["RidesAm"] == "true") & (frame["RidesPm"] == "false")]
    pm_only = frame[(frame["RidesAm"] == "false") & (frame["RidesPm"] == "true")]
    assert not am_only.empty
    assert not pm_only.empty
    assert set(am_only["PMRoute"]) == {""}
    assert set(pm_only["AMRoute"]) == {""}


def test_grades_follow_campus_and_households_share_a_home() -> None:
    frame = roster.realize_roster(sample_frame(), riders_per_route=8, seed=2026)
    south = frame[frame["School"] == "Lamar High School"]
    west = frame[frame["School"] == "Lamar Middle School"]
    north = frame[frame["School"] == "Washington Elementary"]
    assert set(south["Grade"]) <= {"9", "10", "11", "12"}
    assert set(west["Grade"]) <= {"6", "7", "8"}
    assert set(north["Grade"]) <= {"K", "1", "2", "3", "4", "5"}
    assert not south.empty and not west.empty and not north.empty

    sizes = frame.groupby("HomeAddress").size()
    assert sizes.max() <= 3
    assert (sizes > 1).any()
    for address, household in frame.groupby("HomeAddress"):
        assert household["GuardianName"].nunique() == 1
        assert household["GuardianPhone"].nunique() == 1
        assert household["StudentName"].nunique() == len(household)
        assert household["GuardianPhone"].iloc[0].startswith("555-01")
        assert address


def test_exclusion_name_and_address_never_appear() -> None:
    sample = sample_frame(per_corridor=6)
    first = roster.realize_roster(sample, riders_per_route=6, seed=3)
    banned_name = first.iloc[0]["StudentName"]
    banned_address = first.iloc[0]["HomeAddress"]
    second = roster.realize_roster(
        sample,
        riders_per_route=6,
        seed=3,
        excluded_names={banned_name},
        excluded_addresses={banned_address},
    )
    assert banned_name not in set(second["StudentName"])
    assert banned_address.casefold() not in set(second["HomeAddress"].str.casefold())
    assert len(second) == 6 * len(CORRIDORS)


def test_neither_session_is_forced_onto_both_routes() -> None:
    rows = []
    for corridor in CORRIDORS:
        rows.append(
            {
                "corridor": corridor,
                "rides_am": "no",
                "rides_pm": "no",
                "north_miles": 0.0,
                "east_miles": 0.0,
            }
        )
    frame = roster.realize_roster(pd.DataFrame(rows), riders_per_route=1, seed=1)
    assert set(frame["RidesAm"]) == {"true"}
    assert set(frame["RidesPm"]) == {"true"}
    assert set(frame["AMRoute"]) == {f"Lamar {corridor}" for corridor in CORRIDORS}
    assert set(frame["PMRoute"]) == {f"Lamar {corridor}-PM" for corridor in CORRIDORS}


def test_output_path_stays_under_rosters(tmp_path: Path) -> None:
    root = roster.repo_root()
    path = roster.resolve_output_path(None, "2026-2027", root)
    assert (
        path == root / "artifacts" / "rosters" / "Lamar-General-5-Routes-2026-2027.csv"
    )
    with pytest.raises(ValueError):
        roster.resolve_output_path(tmp_path / "out.csv", "2026-2027", root)
    with pytest.raises(ValueError):
        roster.resolve_output_path(
            roster.default_exclusion_path(root), "2026-2027", root
        )


def test_seed_is_a_distribution_not_a_roster() -> None:
    seed = roster.load_seed()
    assert list(seed.columns) == list(roster.SEED_COLUMNS)
    assert len(seed) == 300
    assert set(seed["corridor"]) == set(CORRIDORS)
    assert "StudentName" not in seed.columns
    assert "HomeAddress" not in seed.columns


def test_generate_writes_gitignored_roster_without_fitting(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    sample = sample_frame(4)
    monkeypatch.setattr(roster, "_fit_and_sample", lambda *args, **kwargs: sample)
    monkeypatch.setattr(roster, "load_seed", lambda path=None: sample)
    exclusion = tmp_path / "empty-exclusion.csv"
    exclusion.write_text("StudentName,HomeAddress\n", encoding="utf-8")
    path = roster.generate_lamar_roster(
        exclusion_roster=exclusion,
        riders_per_route=4,
        epochs=1,
    )
    try:
        assert path.parent == roster.repo_root() / "artifacts" / "rosters"
        header = path.read_text(encoding="utf-8").splitlines()[0]
        assert header == ",".join(INTAKE_COLUMNS)
        assert "special needs" not in path.read_text(encoding="utf-8").casefold()
    finally:
        path.unlink(missing_ok=True)
