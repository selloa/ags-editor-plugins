# Cursor Agent Response Contract (Scaffold)

This document defines the first non-destructive Cursor integration contract for `AGS.Plugin.CursorAgent`.

## Goal

Allow AI responses to be validated and translated into existing `PatchEngine` operations, while preserving the current safety behavior:

- preview-first,
- manual apply,
- existing conflict checks,
- undo support.

No automatic script edits are introduced by this scaffold.

## JSON Contract Schema (v1)

The plugin accepts a strict JSON object:

```json
{
  "contract": "ags.cursor.patch-ops",
  "version": 1,
  "operations": [
    {
      "op": "replace",
      "match": "old text",
      "text": "new text"
    }
  ]
}
```

Supported `op` values:

- `replace` -> replace first exact `match` occurrence with `text`
- `add_after` -> insert `text` after first exact `match` occurrence
- `add_before` -> insert `text` before first exact `match` occurrence
- `remove` -> remove first exact `match` occurrence

Legacy plain-text patch format is still supported for backward compatibility.

## Validation Rules

Top-level object:

- must be valid JSON object,
- allowed keys are exactly: `contract`, `version`, `operations`,
- `contract` must be exactly `ags.cursor.patch-ops`,
- `version` must be integer `1`,
- `operations` must be a non-empty array.

Each operation object:

- must be an object,
- `op` must be one of: `replace`, `add_after`, `add_before`, `remove`,
- `match` must be a non-empty string,
- `text` is required and must be a string for `replace`, `add_after`, `add_before`,
- `text` is not allowed for `remove`,
- no extra keys are allowed.

## Failure Handling

All failures remain non-destructive and stop before apply:

1. **Contract parse/validation failure**
   - Preview and Apply show warning: `Patch parse failed: <reason>`.
   - No script content changes.

2. **Patch conflict failure (existing behavior)**
   - Adapter output is passed to existing `PatchEngine.Apply`.
   - If expected `match` text is missing, apply fails with conflict message.

3. **Script conflict failure (existing behavior)**
   - Existing selection/full-script conflict checks still gate manual apply.
   - Undo behavior remains unchanged.

## Implementation Notes

- Parser: `CursorResponseContractParser`
- Adapter: `CursorContractPatchAdapter`
- Integration seam: `CursorAgentPane` `Preview` and `Apply manually` handlers call shared operation parsing method.

This is intended as scaffolding for later Cursor transport integration (response ingestion/UI), not as a final protocol.
