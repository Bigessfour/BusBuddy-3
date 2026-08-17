# Contract: Address Validation + Geocode

**Provider**: Google Address Validation API
**Docs**: https://developers.google.com/maps/documentation/address-validation/overview
**Auth**: `X-Goog-Api-Key` + optional `X-Goog-User-Project: new-coursera-490518`

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

Response mapping:

- Verdict / address completeness → `IsValid`
- `geocode.location` → lat/lon
- `address.formattedAddress` → normalized display
- Missing location + invalid verdict → `GeocodeAsync` returns null

## Error contract

| Condition           | App behavior                                                                |
| ------------------- | --------------------------------------------------------------------------- |
| No API key          | Null coords; UI “mapping not configured”; Serilog Warning                   |
| 403 API not enabled | Configuration error message; no crash                                       |
| 429                 | Backoff once; then fail with retry-later message                            |
| Timeout             | Fail closed for **save validation**; fail open for bulk plot (skip student) |

## Logging

Serilog: `Address validated Deliverable={Deliverable} Precision={Precision} ElapsedMs={ElapsedMs}` — no key, no full street in Information if avoidable (use hash of normalized line at Debug).
