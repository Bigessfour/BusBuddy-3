# Page Audit: `<PageName>`

**Date:** YYYY-MM-DD
**Auditor:** (agent / human)
**Scope:** `<path/to/View.xaml>`
**Mode:** full | bindings-only | save-path-only | syncfusion-only
**Symptom:** (if any)

---

## File graph

| Role             | Path |
| ---------------- | ---- |
| View             |      |
| Code-behind      |      |
| ViewModel        |      |
| Model            |      |
| Service          |      |
| DbContext config |      |
| Migrations       |      |

---

## Executive summary

(2–4 sentences: ship/no-ship, top risk, save-path verdict)

---

## Findings

| Sev | Area | Location  | Finding | Evidence | Recommendation |
| --- | ---- | --------- | ------- | -------- | -------------- |
| P0  |      | file:line |         |          |                |
| P1  |      |           |         |          |                |

---

## Syncfusion control inventory

| Control type | Count | Key properties | Vendor skill              | MCP verified? | Repo-compliant? |
| ------------ | ----- | -------------- | ------------------------- | ------------- | --------------- |
| SfTextBoxExt |       |                | syncfusion-wpf-textboxext | Yes/No        |                 |

### MCP queries run

- `search_docs`: (list queries)

---

## Binding matrix (View ↔ ViewModel)

| Control | Property | Binding | VM member | OK? | Notes |
| ------- | -------- | ------- | --------- | --- | ----- |
|         |          |         |           |     |       |

### Orphans

- **VM without UI:**
- **UI without VM:**

---

## Persistence chain (ViewModel ↔ EF ↔ DB)

| Field | UI bound? | Model property | DB column / FK | Constraints | Normalizer | OK? |
| ----- | --------- | -------------- | -------------- | ----------- | ---------- | --- |
|       |           |                |                |             |            |     |

---

## Save path trace

```
[Save button] → SaveCommand → … → SaveChangesAsync
```

| Step               | Location | Notes |
| ------------------ | -------- | ----- |
| CanExecute         |          |       |
| VM validation      |          |       |
| Service validation |          |       |
| Normalize          |          |       |
| EF write           |          |       |
| Error mapping      |          |       |

---

## WPF / theme / accessibility

- [ ] Resource dictionaries merged
- [ ] DynamicResource brushes (no hardcoded text colors on themed surfaces)
- [ ] AutomationProperties.Name on inputs/buttons
- [ ] Validation templates on required fields

---

## Tests and manual plan

| Test               | Status |
| ------------------ | ------ |
| Build              |        |
| XamlCompliance     |        |
| ViewTests          |        |
| Manual save (add)  |        |
| Manual save (edit) |        |

---

## Enhancement recommendations

1.
2.

---

## Remediation queue (optional)

Ordered list of fixes if user wants implementation next.
