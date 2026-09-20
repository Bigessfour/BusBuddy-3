# Contract: Address Validation + Geocode

**Provider**: Google Address Validation API
**Docs**: https://developers.google.com/maps/documentation/address-validation/overview
**Auth**: `X-Goog-Api-Key` + optional `X-Goog-User-Project: busbuddy-507301`

## BusBuddy interface (Core)

Existing `IAddressValidationService.ValidateAddressAsync` MUST return standardized address when valid.

Existing `IGeocodingService.GeocodeAsync` MUST return `(lat, lon)?` from the same provider result (or cache), never a hash.

Recommended combined internal type (not required to be public):

```text
ValidateAndGeocode(street, city, state, zip) →
  { Ok, FormattedAddress, Lat?, Lon?, Precision, ErrorMessage? }
```

## HTTP (implementer)

`POST https://addressvalidation.googleapis.com/v1:validateAddress`

Request (conceptual):

- `address.regionCode`: `US`
- `address.addressLines`: street + city/state/ZIP
- `enableUspsCass`: true

Response mapping (Google risk-averse checkout: https://developers.google.com/maps/documentation/address-validation/build-validation-logic):

- `verdict.possibleNextAction` FIX, or `validationGranularity` OTHER/ROUTE → not valid, no pin
- `verdict.geocodeGranularity` is the pin accuracy (can be coarser than `validationGranularity`)
- Pin only when `geocodeGranularity` is PREMISE / SUB_PREMISE / PREMISE_PROXIMITY **and** the street is confirmed. Unconfirmed `street_number`/`route`, or `geocode.placeTypes` of only locality/political, is a city centroid — not a pin.
- `uspsData.dpvConfirmation` N → not valid; D / missing subpremise → needs unit; empty DPV is allowed when the verdict is otherwise premise-grade (rural Wiley/Lamar)
- `geocode.location` → lat/lon
- `address.formattedAddress` / `postalAddress` → normalized display
- Missing location + invalid verdict → `GeocodeAsync` returns null
- Geocoding API fallback is not proof the address exists (https://developers.google.com/maps/architecture/geocoding-address-validation). Pin only ROOFTOP + types street_address/premise/subpremise.

Clerk-facing `ErrorMessage` for a rejected pin starts with `Rejected.`, includes `No map pin.`, and names the next click (`Validate Address`). Unconfirmed street/house number is a rejection, not a pending to-do.

## Error contract

| Condition           | App behavior                                                                |
| ------------------- | --------------------------------------------------------------------------- |
| No API key          | Null coords; UI “mapping not configured”; Serilog Warning                   |
| 403 API not enabled | Configuration error message; no crash                                       |
| 429                 | Backoff once; then fail with retry-later message                            |
| Timeout             | Fail closed for **save validation**; fail open for bulk plot (skip student) |

## Logging

Serilog: `Address validated Deliverable={Deliverable} Precision={Precision} ElapsedMs={ElapsedMs}` — no key, no full street in Information if avoidable (use hash of normalized line at Debug).
