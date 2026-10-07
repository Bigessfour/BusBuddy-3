"""Public street ranges around Lamar, Colorado.

House numbers are invented inside each range. This catalog has no household
names and is not a student roster.
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class StreetRange:
    """One public street and the house numbers the generator may use."""

    name: str
    low: int
    high: int
    city: str = "Lamar"
    state: str = "CO"
    zip_code: str = "81052"

    def __post_init__(self) -> None:
        if self.zip_code != "81052":
            raise ValueError("Lamar exercise streets use ZIP 81052")
        if self.low < 1 or self.low > self.high:
            raise ValueError(f"Invalid house range for {self.name}")


STREETS: dict[str, tuple[StreetRange, ...]] = {
    "North": (
        StreetRange("N Main St", 300, 900),
        StreetRange("N 5th St", 200, 800),
        StreetRange("N 6th St", 200, 800),
        StreetRange("Park St", 100, 600),
        StreetRange("Maple St", 100, 600),
    ),
    "South": (
        StreetRange("S 11th St", 100, 700),
        StreetRange("S 6th St", 100, 600),
        StreetRange("S Main St", 800, 1600),
        StreetRange("Savage Ave", 100, 500),
        StreetRange("Hickory St", 100, 500),
    ),
    "East": (
        StreetRange("E Olive St", 200, 900),
        StreetRange("E Pearl St", 200, 900),
        StreetRange("E Parmenter St", 200, 800),
        StreetRange("E Hickory St", 100, 500),
    ),
    "West": (
        StreetRange("W Olive St", 200, 900),
        StreetRange("W Pearl St", 200, 900),
        StreetRange("W Parmenter St", 200, 800),
        StreetRange("W Maple St", 100, 600),
    ),
    # County Road 5 and Highway 287 geocode outside the Census outline for
    # Lamar School District RE-2 (NCES GEOID 0805220). County Road 7 and 8
    # fall inside that outline. Olive and 6th are in-town streets that
    # Address Validation will pin at a building.
    "Rural": (
        StreetRange("County Road 7", 100, 400),
        StreetRange("County Road 8", 100, 400),
        StreetRange("E Olive St", 200, 900),
        StreetRange("S 6th St", 100, 600),
    ),
}
