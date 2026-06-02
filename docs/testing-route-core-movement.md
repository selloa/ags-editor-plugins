# AGS Cursor Plugin Test Route (`core_movement.asc`)

This document is a practical test script for your current plugin state, using:

- `C:\Users\selloa\Desktop\Starterpack 2026\core_movement.asc`

---

## Current Functionality (Now)

The plugin currently behaves as a **safe patch executor** with manual review:

- `Capture context` reads from AGS script tabs.
  - If text is selected, capture is **selection mode**.
  - If no text is selected, capture is **full script mode**.
- `Template sel` generates a `REPLACE` patch template from the captured selection.
- `Preview` parses the patch text and reports operation counts.
- `Apply manually`:
  - parses and validates patch text,
  - conflict-checks against captured source,
  - applies to the real AGS script buffer if safe.
- `Undo` reverts the last plugin-applied change if no further conflict is detected.
- Patch parser is strict: each field block requires a closing `END`.

## Intended Functionality (Later)

Target direction is to keep the same safety model, but improve automation:

- Agent-generated `NEW` content auto-filled from AI output (schema-validated).
- Better UX guidance (button order and step hints).
- Potentially richer patch operations and clearer diff visualization.
- Maintain explicit review and safe apply/undo behavior.

---

## Test Setup

1. Open AGS and load a project containing `core_movement.asc`.
2. Open `core_movement.asc` in a script tab.
3. Open plugin panel from `Cursor Agent`.
4. Keep this document open and copy/paste blocks exactly.

---

## Scenario 1: Selection Replace (Happy Path)

### 1A) Select source text in script

In `core_movement.asc`, select exactly this line:

```ags
return Absolute (point1 - point2);
```

Then click `Capture context`.

Expected:
- Target shows selection mode.
- `Original selection` contains exactly that line.

### 1B) Generate template

Click `Template sel`.

Expected:
- `Proposed output` is a `REPLACE` template with `OLD` filled.

### 1C) Paste this `NEW` patch content

Replace the full `Proposed output` with:

```text
REPLACE
OLD:
return Absolute (point1 - point2);
END
NEW:
return Absolute(point1 - point2);
END
```

Click `Preview`, then `Apply manually`.

Expected:
- Preview parse success.
- Script line changes in AGS.

### 1D) Undo

Click `Undo`.

Expected:
- Script line returns to original.

---

## Scenario 2: Multi-Line Selection Replace

### 2A) Select this block in script

```ags
if (value < 0) value = - value;
return value;
```

Click `Capture context`, then `Template sel`.

### 2B) Paste patch

```text
REPLACE
OLD:
if (value < 0) value = - value;
return value;
END
NEW:
if (value < 0) value = -value;
return value;
END
```

Click `Preview`, then `Apply manually`.

Expected:
- Both selected lines are replaced.

Click `Undo` and verify rollback.

---

## Scenario 3: Conflict Detection (Selection Changed)

1. Select and capture this line:

```ags
if (dx == DIR_DOWN) dy = 1;
```

2. Before clicking `Apply manually`, manually edit that same line in the script tab (for example add one space or comment).
3. Use this patch:

```text
REPLACE
OLD:
if (dx == DIR_DOWN) dy = 1;
END
NEW:
if (dx == DIR_DOWN) {
  dy = 1;
}
END
```

Expected:
- Apply is rejected with a conflict warning.
- No unintended overwrite.

---

## Scenario 4: Parse Validation (Missing END)

Capture any selection and use this intentionally invalid patch:

```text
REPLACE
OLD:
return MovePlayerEx (x, y, 0);
END
NEW:
return MovePlayerEx(x, y, 0);
```

Expected:
- Parse error (missing `END`).
- No apply.

---

## Scenario 5: Full Script Mode (No Selection)

1. Click in `core_movement.asc` without selecting text.
2. Click `Capture context`.
3. Confirm target indicates full script mode.
4. Use a tiny replace patch that exists in full script text:

```text
REPLACE
OLD:
function PlacePC (int x, int y, int dir){
END
NEW:
function PlacePC(int x, int y, int dir){
END
```

Expected:
- Apply succeeds only if script still matches captured full text state.
- If script changed after capture, apply should fail with conflict warning.

---

## Notes Template (for your findings)

Use this per scenario while testing:

```text
Scenario:
Expected:
Actual:
Error message (if any):
Repro steps:
Frequency:
Notes:
```

---

## Recommended Testing Order

1. Scenario 1 (happy path)
2. Scenario 2 (multi-line)
3. Scenario 3 (selection conflict)
4. Scenario 4 (parser strictness)
5. Scenario 5 (full script conflict behavior)

