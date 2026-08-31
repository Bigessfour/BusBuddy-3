# Contract: Assign Fitness / Toast

**Consumers**: Students / Route ViewModels (Syncfusion toast or status banner)

## AssignFitnessResult

| Field             | Meaning                                                                  |
| ----------------- | ------------------------------------------------------------------------ |
| Allowed           | If false, UI must not call assign persist (unless seating override path) |
| Severity          | `None`, `Warn`, `Block`                                                  |
| Reasons           | Human-readable strings for toast body                                    |
| SuggestedRouteIds | Prefer existing routes                                                   |
| SuggestNewRoute   | True when past district threshold — toast offers “create route”          |

## Policy (locked)

| Condition                                                         | Behavior                                     |
| ----------------------------------------------------------------- | -------------------------------------------- |
| Assigned count + 1 &gt; bus `SeatingCapacity` and no override     | **Block** + toast                            |
| Estimated arrival after school StartTime (or soft MaxRideMinutes) | **Warn** + allow                             |
| Student would be geo outlier vs route cell / gap threshold        | **Warn** + allow                             |
| Explicit seating override recorded                                | Allow Block seating case; Serilog `Override` |

## UI contract

- Toast/status must show at least one `Reasons` line and, when present, suggested route names.
- Warn-and-allow: proceed after toast without modal unless clerk cancels.
- Block: no silent assign; optional Syncfusion dialog for override when `AllowSeatingOverride` is true.
