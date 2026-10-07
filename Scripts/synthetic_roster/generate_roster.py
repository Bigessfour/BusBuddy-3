"""Fictional Lamar general-education roster for five corridors.

Trains ``ydata-synthetic`` CTGAN on ``seed_distribution.csv`` (corridor, grade
band, AM/PM flags, and mile offsets only). Names and street addresses are
applied afterward. The special-needs roster is an exclusion list when it
exists on disk. This module never writes that file and never trains on it.

Import the CSV with ``BusBuddy.DbPrep -- import-roster``. The importer does
not read coordinates; pins come from Google Address Validation. Do not commit
the generated CSV (``specs/students.md``).

Fit CTGAN with Python 3.11 and ``ydata-synthetic==1.4.0``. Version 2 removed
``RegularSynthesizer``, and TensorFlow does not publish 3.14 wheels.
"""

from __future__ import annotations

import argparse
import csv
import math
import random
from pathlib import Path

import pandas as pd
from faker import Faker
from lamar_streets import STREETS, StreetRange

CORRIDORS: tuple[str, ...] = ("North", "South", "East", "West", "Rural")

DEFAULT_SCHOOL_BY_CORRIDOR: dict[str, str] = {
    "North": "Washington Elementary",
    "South": "Lamar High School",
    "East": "Parkview Elementary",
    "West": "Lamar Middle School",
    "Rural": "Washington Elementary",
}

# Named columns from Documentation/STUDENT-ROSTER-INTAKE.md. RidesAm and
# RidesPm are set explicitly so the importer does not infer eligibility.
ROSTER_COLUMNS: tuple[str, ...] = (
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
)

SEED_COLUMNS: tuple[str, ...] = (
    "corridor",
    "grade_band",
    "rides_am",
    "rides_pm",
    "north_miles",
    "east_miles",
)

CATEGORICAL_COLUMNS: tuple[str, ...] = (
    "corridor",
    "grade_band",
    "rides_am",
    "rides_pm",
)
NUMERIC_COLUMNS: tuple[str, ...] = ("north_miles", "east_miles")

SPECIAL_NEEDS_ROSTER_NAME = "AM-Bus-5-Special-Needs-2026-2027.csv"

_TRUE = {"yes", "true", "y", "1"}
_FALSE = {"no", "false", "n", "0"}


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def seed_path() -> Path:
    return Path(__file__).resolve().with_name("seed_distribution.csv")


def default_exclusion_path(root: Path | None = None) -> Path:
    return (root or repo_root()) / "artifacts" / "rosters" / SPECIAL_NEEDS_ROSTER_NAME


def am_route_name(corridor: str) -> str:
    return f"Lamar {corridor}"


def pm_route_name(corridor: str) -> str:
    return f"Lamar {corridor}-PM"


def resolve_output_path(
    output_path: Path | None,
    school_year: str,
    root: Path | None = None,
) -> Path:
    """Roster files stay under artifacts/rosters and never replace special needs."""
    repo = root or repo_root()
    roster_dir = (repo / "artifacts" / "rosters").resolve()
    if output_path is None:
        candidate = roster_dir / f"Lamar-General-5-Routes-{school_year}.csv"
    else:
        candidate = Path(output_path)
        if not candidate.is_absolute():
            candidate = repo / candidate
        candidate = candidate.resolve()
    if candidate.parent != roster_dir:
        raise ValueError(f"Roster output must be under {roster_dir}")
    folded = candidate.name.casefold()
    if (
        folded == SPECIAL_NEEDS_ROSTER_NAME.casefold()
        or "special needs" in folded
        or "special-needs" in folded
    ):
        raise ValueError("Refusing to write the special-needs roster")
    return candidate


def load_exclusion(path: Path | None) -> tuple[set[str], set[str]]:
    """Student names and home addresses to keep off the generated sheet."""
    if path is None or not path.is_file():
        return set(), set()
    with path.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        if not reader.fieldnames:
            return set(), set()
        fields = {(name or "").strip() for name in reader.fieldnames}
        if "StudentName" not in fields and "HomeAddress" not in fields:
            raise ValueError(f"{path} has no StudentName or HomeAddress column")
        names: set[str] = set()
        addresses: set[str] = set()
        for row in reader:
            name = (row.get("StudentName") or "").strip()
            address = (row.get("HomeAddress") or "").strip()
            if name:
                names.add(name.casefold())
            if address:
                addresses.add(address.casefold())
        return names, addresses


def load_seed(path: Path | None = None) -> pd.DataFrame:
    frame = pd.read_csv(path or seed_path())
    missing = [column for column in SEED_COLUMNS if column not in frame.columns]
    if missing:
        raise ValueError(f"Seed is missing columns: {', '.join(missing)}")
    return frame.loc[:, list(SEED_COLUMNS)].copy()


def schools_for(mapping: dict[str, str] | None) -> dict[str, str]:
    """Placeholder campus names. Replace them with Destinations.Name before import."""
    chosen = dict(DEFAULT_SCHOOL_BY_CORRIDOR)
    if not mapping:
        return chosen
    unknown = sorted(set(mapping) - set(CORRIDORS))
    if unknown:
        raise ValueError(f"Unknown corridor(s): {', '.join(unknown)}")
    for corridor, school in mapping.items():
        text = str(school).strip()
        if not text:
            raise ValueError(f"School name for {corridor} is blank")
        chosen[corridor] = text
    return chosen


def grades_for_school(school: str) -> tuple[str, ...]:
    lowered = school.casefold()
    if "high" in lowered:
        return ("9", "10", "11", "12")
    if "middle" in lowered:
        return ("6", "7", "8")
    return ("K", "1", "2", "3", "4", "5")


def _canonical_corridor(value: object) -> str | None:
    text = str(value).strip().casefold()
    if not text or text == "nan":
        return None
    for corridor in CORRIDORS:
        if text == corridor.casefold():
            return corridor
    return None


def _as_float(value: object) -> float:
    try:
        number = float(value)  # type: ignore[arg-type]
    except (TypeError, ValueError):
        return 0.0
    if math.isnan(number) or math.isinf(number):
        return 0.0
    return number


def _as_bool(value: object) -> bool | None:
    if value is None:
        return None
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if isinstance(value, float) and math.isnan(value):
            return None
        return float(value) >= 0.5
    text = str(value).strip().casefold()
    if text in _TRUE:
        return True
    if text in _FALSE:
        return False
    try:
        return float(text) >= 0.5
    except ValueError:
        return None


def _decode_categories(sample: pd.DataFrame, seed: pd.DataFrame) -> pd.DataFrame:
    """Map CTGAN label codes back onto the seed's category text."""
    decoded = sample.copy()
    for column in CATEGORICAL_COLUMNS:
        if column not in decoded.columns:
            continue
        labels = sorted(seed[column].astype(str).unique())
        known = set(labels)
        series = decoded[column]
        text = series.astype(str).str.strip()
        if text.str.casefold().isin({label.casefold() for label in known}).mean() > 0.5:
            decoded[column] = text
            continue
        codes = pd.to_numeric(series, errors="coerce").round()
        mapped: list[str] = []
        for code, raw in zip(codes, text, strict=True):
            if pd.notna(code):
                index = int(code)
                mapped.append(labels[index] if 0 <= index < len(labels) else raw)
            else:
                mapped.append(raw)
        decoded[column] = mapped
    return decoded


def _household_sizes(count: int, rng: random.Random) -> list[int]:
    sizes: list[int] = []
    remaining = count
    while remaining:
        size = min(rng.choice((1, 1, 2, 2, 3)), remaining)
        sizes.append(size)
        remaining -= size
    return sizes


def _house_number(street: StreetRange, slot: int) -> int:
    span = street.high - street.low
    number = street.low + (slot * 4) % (span + 1)
    if number % 2:
        number += 1
    if number > street.high:
        number = street.high if street.high % 2 == 0 else street.high - 1
    if number < street.low:
        number = street.low if street.low % 2 == 0 else street.low + 1
    return number


def _choose_address(street: StreetRange, slot: int, used: set[str]) -> str:
    number = _house_number(street, slot)
    for _ in range(60):
        if number > street.high:
            number = street.low if street.low % 2 == 0 else street.low + 1
        address = f"{number} {street.name}"
        if address.casefold() not in used:
            return address
        number += 2
    raise RuntimeError(f"No free house number on {street.name}")


def _assign_address(
    corridor: str, slot: int, used: set[str]
) -> tuple[str, StreetRange]:
    streets = STREETS[corridor]
    last_error: Exception | None = None
    for offset in range(len(streets)):
        street = streets[(slot + offset) % len(streets)]
        try:
            return _choose_address(street, slot + offset, used), street
        except RuntimeError as error:
            last_error = error
    raise RuntimeError(f"No free address in {corridor}") from last_error


def _take_token(draw, used: set[str], excluded: set[str]) -> str:
    for _ in range(300):
        value = draw()
        key = value.casefold()
        if key not in used and key not in excluded:
            used.add(key)
            return value
    raise RuntimeError("Could not draw a unique fictional name")


def _select_rows(
    sample: pd.DataFrame, riders_per_route: int
) -> dict[str, list[dict[str, object]]]:
    grouped: dict[str, list[dict[str, object]]] = {
        corridor: [] for corridor in CORRIDORS
    }
    for record in sample.to_dict(orient="records"):
        corridor = _canonical_corridor(record.get("corridor"))
        if corridor is not None:
            grouped[corridor].append(record)
    selected: dict[str, list[dict[str, object]]] = {}
    for corridor, rows in grouped.items():
        if not rows:
            rows = [
                {
                    "corridor": corridor,
                    "rides_am": "yes",
                    "rides_pm": "yes",
                    "north_miles": 0.0,
                    "east_miles": 0.0,
                }
            ]
        rows = sorted(
            rows,
            key=lambda row: (
                _as_float(row.get("north_miles")),
                _as_float(row.get("east_miles")),
            ),
        )
        selected[corridor] = [
            rows[index % len(rows)] for index in range(riders_per_route)
        ]
    return selected


def realize_roster(
    sample: pd.DataFrame,
    *,
    riders_per_route: int = 36,
    school_year: str = "2026-2027",
    school_by_corridor: dict[str, str] | None = None,
    excluded_names: set[str] | None = None,
    excluded_addresses: set[str] | None = None,
    seed: int = 2026,
) -> pd.DataFrame:
    """Turn synthesizer rows into an importer-ready general-ed roster.

    Special-needs flags stay false. Pickup mode is Home because a catalog stop
    without a published PickupStopId is skipped on import. Each corridor is
    filled to ``riders_per_route`` after exclusion drops.
    """
    if riders_per_route < 1:
        raise ValueError("riders_per_route must be at least 1")
    schools = schools_for(school_by_corridor)
    excluded_name_keys = {name.casefold() for name in (excluded_names or set())}
    used_addresses = {address.casefold() for address in (excluded_addresses or set())}
    used_names: set[str] = set()
    faker = Faker("en_US")
    faker.seed_instance(seed)
    rng = random.Random(seed)
    records: list[dict[str, str]] = []
    phone_index = 0

    for corridor, rows in _select_rows(sample, riders_per_route).items():
        school = schools[corridor]
        grades = grades_for_school(school)
        morning = am_route_name(corridor)
        afternoon = pm_route_name(corridor)
        cursor = 0
        for household_slot, size in enumerate(_household_sizes(len(rows), rng)):
            household = rows[cursor : cursor + size]
            cursor += size
            last_name = _take_token(faker.last_name, used_names, excluded_name_keys)
            # Last names are only reserved while drawing the household. A later
            # child may legally share the surname; uniqueness is on the full name.
            used_names.discard(last_name.casefold())
            address, street = _assign_address(corridor, household_slot, used_addresses)
            used_addresses.add(address.casefold())
            guardian_phone = f"555-01{phone_index % 100:02d}"
            emergency_phone = f"555-02{phone_index % 100:02d}"
            phone_index += 1
            guardian = _take_token(
                lambda last=last_name: f"{faker.first_name()} {last}",
                used_names,
                excluded_name_keys,
            )
            emergency = _take_token(
                lambda last=last_name: f"{faker.first_name()} {last}",
                used_names,
                excluded_name_keys,
            )
            for child_index, source in enumerate(household):
                rides_am = _as_bool(source.get("rides_am"))
                rides_pm = _as_bool(source.get("rides_pm"))
                if rides_am is False and rides_pm is False:
                    rides_am, rides_pm = True, True
                else:
                    if rides_am is None:
                        rides_am = True
                    if rides_pm is None:
                        rides_pm = True
                student = _take_token(
                    lambda last=last_name: f"{faker.first_name()} {last}",
                    used_names,
                    excluded_name_keys,
                )
                grade = grades[(household_slot + child_index) % len(grades)]
                records.append(
                    {
                        "StopNumber": "",
                        "PickupTime": "",
                        "StudentName": student,
                        "Grade": grade,
                        "School": school,
                        "SchoolYear": school_year,
                        "HomeAddress": address,
                        "City": street.city,
                        "State": street.state,
                        "Zip": street.zip_code,
                        "GuardianName": guardian,
                        "GuardianPhone": guardian_phone,
                        "HomePhone": guardian_phone,
                        "EmergencyContactName": emergency,
                        "EmergencyContactPhone": emergency_phone,
                        "PickupMode": "Home",
                        "PickupStopId": "",
                        "AMRoute": morning if rides_am else "",
                        "PMRoute": afternoon if rides_pm else "",
                        "RidesAm": "true" if rides_am else "false",
                        "RidesPm": "true" if rides_pm else "false",
                        "RequiresSpecialNeedsBus": "false",
                        "RequiresAide": "false",
                        "RequiresWheelchair": "false",
                        "RequiresSeatBelt": "false",
                        "HasMedicalNeeds": "false",
                        "Active": "true",
                        "TransportationNotes": "",
                    }
                )

    frame = pd.DataFrame.from_records(records, columns=list(ROSTER_COLUMNS))
    if len(frame) != riders_per_route * len(CORRIDORS):
        raise RuntimeError("Roster did not fill every corridor")
    return frame


def write_roster(
    frame: pd.DataFrame, output_path: Path, root: Path | None = None
) -> Path:
    path = resolve_output_path(output_path, school_year="unused", root=root)
    # resolve_output_path treats a concrete path as the destination. The school
    # year argument is only used when output_path is None, which it is not here.
    path.parent.mkdir(parents=True, exist_ok=True)
    frame.to_csv(path, index=False)
    return path


def _fit_and_sample(
    seed_frame: pd.DataFrame, sample_size: int, epochs: int, seed: int
) -> pd.DataFrame:
    """Fit CTGAN. Imported lazily so roster tests do not require TensorFlow."""
    import numpy as np
    from ydata_synthetic.synthesizers import ModelParameters, TrainParameters
    from ydata_synthetic.synthesizers.regular import RegularSynthesizer

    random.seed(seed)
    np.random.seed(seed)
    try:
        import tensorflow as tf

        tf.random.set_seed(seed)
    except Exception:
        pass

    training = seed_frame.copy()
    for column in CATEGORICAL_COLUMNS:
        training[column] = training[column].astype(str)
    for column in NUMERIC_COLUMNS:
        training[column] = pd.to_numeric(training[column], errors="coerce").fillna(0.0)

    # CTGAN 1.4 requires an even batch size divisible by pac (default 10).
    batch_size = 50
    if len(training) < batch_size:
        raise ValueError(
            f"Seed has {len(training)} rows; CTGAN needs at least {batch_size}."
        )
    model_parameters = ModelParameters(batch_size=batch_size, lr=2e-4, betas=(0.5, 0.9))
    train_arguments = TrainParameters(epochs=epochs)
    synthesizer = RegularSynthesizer(
        modelname="ctgan", model_parameters=model_parameters
    )
    synthesizer.fit(
        data=training,
        train_arguments=train_arguments,
        num_cols=list(NUMERIC_COLUMNS),
        cat_cols=list(CATEGORICAL_COLUMNS),
    )
    sampled = synthesizer.sample(sample_size)
    if not isinstance(sampled, pd.DataFrame):
        sampled = pd.DataFrame(sampled, columns=list(SEED_COLUMNS))
    return _decode_categories(sampled, training)


def generate_lamar_roster(
    *,
    output_path: Path | None = None,
    exclusion_roster: Path | None = None,
    school_by_corridor: dict[str, str] | None = None,
    riders_per_route: int = 36,
    school_year: str = "2026-2027",
    seed: int = 2026,
    epochs: int = 80,
) -> Path:
    """Sample a general-ed roster and write it under artifacts/rosters.

    ``school_by_corridor`` defaults are placeholders. Pass the live
    ``Destinations.Name`` values before a real import or campuses stay unlinked.
    """
    if epochs < 1:
        raise ValueError("epochs must be at least 1")
    root = repo_root()
    destination = resolve_output_path(output_path, school_year, root)
    exclusion_path = (
        exclusion_roster
        if exclusion_roster is not None
        else default_exclusion_path(root)
    )
    if exclusion_path.is_file() and destination.resolve() == exclusion_path.resolve():
        raise ValueError("Refusing to overwrite the special-needs roster")
    excluded_names, excluded_addresses = load_exclusion(
        exclusion_path if exclusion_path.is_file() else None
    )
    training = load_seed()
    sample_size = riders_per_route * len(CORRIDORS) * 3
    sampled = _fit_and_sample(training, sample_size, epochs, seed)
    roster = realize_roster(
        sampled,
        riders_per_route=riders_per_route,
        school_year=school_year,
        school_by_corridor=school_by_corridor,
        excluded_names=excluded_names,
        excluded_addresses=excluded_addresses,
        seed=seed,
    )
    return write_roster(roster, destination, root)


def _parse_school_args(pairs: list[str] | None) -> dict[str, str] | None:
    if not pairs:
        return None
    mapping: dict[str, str] = {}
    for pair in pairs:
        if "=" not in pair:
            raise SystemExit(f"Expected Corridor=School Name, got {pair!r}")
        corridor, school = pair.split("=", 1)
        mapping[corridor.strip()] = school.strip()
    return mapping


def main(argv: list[str] | None = None) -> Path:
    parser = argparse.ArgumentParser(
        description="Write a fictional Lamar general-ed roster CSV."
    )
    parser.add_argument("--output", type=Path, default=None)
    parser.add_argument("--exclusion", type=Path, default=None)
    parser.add_argument("--riders-per-route", type=int, default=36)
    parser.add_argument("--school-year", default="2026-2027")
    parser.add_argument("--seed", type=int, default=2026)
    parser.add_argument("--epochs", type=int, default=80)
    parser.add_argument(
        "--school",
        action="append",
        default=None,
        help="Corridor=Destinations.Name (repeat for each corridor). Defaults are placeholders.",
    )
    args = parser.parse_args(argv)
    path = generate_lamar_roster(
        output_path=args.output,
        exclusion_roster=args.exclusion,
        school_by_corridor=_parse_school_args(args.school),
        riders_per_route=args.riders_per_route,
        school_year=args.school_year,
        seed=args.seed,
        epochs=args.epochs,
    )
    print(path)
    return path


if __name__ == "__main__":
    main()
