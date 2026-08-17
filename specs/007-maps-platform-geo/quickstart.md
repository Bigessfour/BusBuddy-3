# Quickstart: 007 Maps Platform Geo

Validation guide after implementation. Do not put secrets in the repo.

## Prerequisites

- Maps APIs enabled on `new-coursera-490518`: Address Validation, Routes
- Passwords / env: `GOOGLE_MAPS_API_KEY` (restricted)
- Local: `dotnet` 9, `EnableWindowsTargeting` on Mac
- Optional Windows VM for WPF map smoke

## 1. Unit tests (no live key)

```bash
dotnet test BusBuddy.sln -c Release -p:EnableWindowsTargeting=true \
  --filter "FullyQualifiedName~GoogleMaps|FullyQualifiedName~RoutingService|FullyQualifiedName~OfflineGeocoding"
```

Expect: validation/geocode/routing fakes pass; production DI tests assert `IGeocodingService` is **not** `OfflineGeocodingService`.

## 2. Live key smoke (optional, developer machine)

```bash
export GOOGLE_MAPS_API_KEY=...   # from Passwords, never echo
dotnet run --project .github/scripts/MapsConnectionProbe
```

Expect: Address Validation HTTP 200 for a Wiley-area sample; Routes HTTP 200 for school → one stop. Fail clearly if APIs disabled.

## 3. App startup (no EE)

- No `GEE_*` required
- Serilog: no `Obtained Earth Engine access token` / `Google Earth Engine configured`
- With key: `Google Maps client registered KeyKind=present`
- Without key: `Google Maps client registered KeyKind=missing`

## 4. Clerk path (Windows VM)

1. Open Students → add/edit address in Wiley
2. Validate/save → success + coordinates
3. Open map view → marker at that home, not random scatter
4. Route with two stops → refresh path → polyline on SfMap

## 5. Docs gate

- [AGENTS.md](../../AGENTS.md) Maps key + billing project; EE unused
- [Documentation/GCP-GEE-SECRETS-AND-AUTH.md](../../Documentation/GCP-GEE-SECRETS-AND-AUTH.md) rewritten for Maps
- Architecture map: GeoDataService / Maps / ShapefileEligibility — no GoogleEarthEngine node

## 6. RAG

```bash
python -m rag.index
```

After doc/constitution/spec edits.
