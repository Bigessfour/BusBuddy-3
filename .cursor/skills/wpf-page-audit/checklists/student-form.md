# Checklist: StudentForm (add / edit student)

**View:** `BusBuddy.WPF/Views/Student/StudentForm.xaml`
**ViewModel:** `BusBuddy.WPF/ViewModels/Student/StudentFormViewModel.cs`
**Model:** `BusBuddy.Core/Models/Student.cs`
**Save:** `SaveStudentAsync` → `IStudentService.AddStudentAsync` / `UpdateStudentAsync` (or direct EF fallback)

Use with [../SKILL.md](../SKILL.md). Mark each item Pass / Fail / N/A.

---

## P0 — Save path (runtime errors)

| #   | Check                                                                              | Pass? | Notes |
| --- | ---------------------------------------------------------------------------------- | ----- | ----- |
| 1   | Save uses UI thread (no `Task.Run` on collections in VM ctor)                      |       |       |
| 2   | `Student.StudentName` and `Student.Grade` bound TwoWay before save                 |       |       |
| 3   | `SelectedSchoolDestination` syncs `Student.DestinationId` + `Student.School`       |       |       |
| 4   | `SelectedPickupStop` syncs `Student.PickupStopId` (null when cleared, not 0)       |       |       |
| 5   | `StudentRecordNormalizer.NormalizeForPersistence` runs before EF (VM + DbContext)  |       |       |
| 6   | `DateOfBirth` UTC kind (Postgres `timestamptz`)                                    |       |       |
| 7   | `EmergencyContactPhone` binding maps to `EmergencyPhone` column (NotMapped alias)  |       |       |
| 8   | AM/PM route strings exist in `Routes` table OR are empty (service validates names) |       |       |
| 9   | Duplicate `StudentNumber` surfaces on `StudentNumber` field not global only        |       |       |
| 10  | Address validation failure blocks save with `HomeAddress` field error              |       |       |
| 11  | DB unavailable → `DatabaseUserMessage` global error, no partial write              |       |       |
| 12  | Success closes via `RequestClose(true)` and sends `StudentSavedMessage`            |       |       |

### Common save failure signatures

| Symptom                                          | Likely layer                          | Inspect                                      |
| ------------------------------------------------ | ------------------------------------- | -------------------------------------------- |
| "Invalid grade level"                            | `StudentService.ValidateStudentAsync` | `AvailableGrades` vs valid grades array      |
| "AM/PM Route does not exist"                     | Service route check                   | Combo `SelectedItem` vs free-text route name |
| FK violation on `DestinationId` / `PickupStopId` | EF                                    | Normalizer; stale combo selection            |
| DateTime / timestamp error                       | Postgres                              | `DateOfBirth`, `CreatedDate` Kind            |
| Student number already in use                    | DB unique + service                   | Field error mapping in catch                 |
| Blank save / button no-op                        | `CanSaveStudent`                      | Name + grade required                        |

---

## Syncfusion inventory (StudentForm)

Run: `rg -o 'syncfusion:[A-Za-z0-9]+' BusBuddy.WPF/Views/Student/StudentForm.xaml | sort -u`

| Control              | MCP verified? | Repo pattern                                                   |
| -------------------- | ------------- | -------------------------------------------------------------- |
| ChromelessWindow     |               | Theme via SfSkinManager in code-behind                         |
| SfTextBoxExt         |               | `ValidatedSfTextBoxStyle`, Watermark, AutomationProperties     |
| ComboBoxAdv          |               | `StudentFormComboBoxStyle`, `IsEditable` only when intentional |
| SfDatePicker         |               | `AllowNull`, `FormatString`, UTC on save                       |
| SfMaskedEdit         |               | Phone/zip masks match normalizer output                        |
| ButtonAdv            |               | `Label` + `Command`, `ButtonAdvTextOnly.xaml`                  |
| CheckBoxAdv / Toggle |               | TwoWay to bool model properties                                |

---

## Binding matrix (critical fields)

| UI control         | Binding path                | VM / model                               |
| ------------------ | --------------------------- | ---------------------------------------- |
| StudentNameTextBox | `Student.StudentName`       | `Student` on VM                          |
| GradeComboBox      | `Student.Grade`             | `AvailableGrades`                        |
| SchoolComboBox     | `SelectedSchoolDestination` | `Destination` object                     |
| Pickup stop combo  | `SelectedPickupStop`        | `PickupStop` object                      |
| DateOfBirthPicker  | `Student.DateOfBirth`       | `DateTime?` UTC on save                  |
| Save button        | `SaveCommand`               | `AsyncRelayCommand` → `SaveStudentAsync` |

---

## EF / Students table

Source: `BusBuddyDbContext` → `Student` entity configuration.

| Property      | Required | Max len | FK             |
| ------------- | -------- | ------- | -------------- |
| StudentName   | Yes      | 100     |                |
| Grade         | No       | 20      |                |
| DestinationId | No       |         | → Destinations |
| PickupStopId  | No       |         | → PickupStops  |
| State         | No       | 2       |                |
| Zip           | No       | 10      |                |

---

## Manual test script (Windows VM)

1. Open Students → Add Student
2. Enter name + grade only → Save → expect success
3. Add with duplicate student number → expect field error
4. Select school from combo → Save → `DestinationId` set in DB
5. Select invalid AM route (if editable) → expect validation error
6. Edit existing student → change grade → Save
7. Cancel with dirty form → confirm discard prompt

---

## Known enhancement targets

- Add `StudentFormViewTests.cs` (binding smoke like `AnalyticsDashboardViewTests`)
- Integration test: `AddStudentAsync` minimal record against Testcontainers Postgres
- Align school time edit (`SaveSchoolTimesCommand`) with main save (two-phase UX)
