# Feature Refinement Plan v1: Tabs, ESC Startup, Multi-Close Buttons

## Context

The current HUD sample flow is close, but three UX gaps remain:

1. Tab behavior needs refinement and designer control.
2. `ESC` does not open pause at startup (works only after pause has been opened once by button/controller path).
3. Some popup windows expose multiple close/back affordances, but only one is consistently wired.

This plan is scoped to feature polish and consistency. It does not reopen architecture refactors.

---

## Scope

In scope:

1. Inventory tab UX and wiring improvements.
2. Startup `ESC` pause behavior fix.
3. Unified close behavior for all close/back buttons in sample popups.
4. Setup tool updates so regenerated scenes are deterministic.

Out of scope:

1. Services architecture changes (`Services`, `BaseSceneServicesInstaller`, `GlobalServicesInstaller`).
2. New runtime/controller systems.
3. Non-HUD sample feature additions.

---

## Assumptions

1. We can rely on regenerating scenes through `SampleSceneSetupTool`.
2. Prefabs may contain legacy onClick persistent calls that must be neutralized by setup binding.
3. Input System remains active (no legacy `StandaloneInputModule` path).

---

## Execution Order

1. Stabilize `ESC` startup behavior first.
2. Refine tabs and ensure deterministic default state.
3. Normalize popup close button handling across all modal examples.
4. Rewire/setup verification pass.

---

## Phase 0 — Baseline Snapshot

Before edits:

1. Capture current behavior in play mode:
   - On fresh play start, press `ESC` before pressing pause button.
   - Open inventory and cycle tabs repeatedly.
   - Open each sample modal and click every visible close/back affordance.
2. Record which specific prefabs/windows show duplicate or unbound close actions.

---

## Phase 1 — Fix ESC Not Working at Startup

### Target
- `Assets/Scripts/Core/UI/MenuInputRouter.cs`

### Change Plan

1. Treat cancel (`ESC`) as pause-open when no menu is currently open.
2. Keep existing back behavior when a menu is open (`PopBack`), but never pop the root gameplay HUD.
3. Add a same-frame guard so combined pause/cancel signals do not open and immediately close.
4. Keep controller pause (`Start`) behavior unchanged.

### Acceptance Criteria

1. Fresh play mode: pressing `ESC` opens pause immediately.
2. Pressing `ESC` again closes pause.
3. With modal stack open: `ESC` pops top menu in correct order.
4. No requirement to click pause button first.

---

## Phase 2 — Tabs Refinement

### Targets
- `Assets/Scripts/Samples/InventoryMenuView.cs`
- `Assets/Editor/SampleSceneSetupTool.cs` (if new tab fields need auto-wiring)

### Change Plan

1. Make tab state explicit and designer-friendly:
   - Add `initialTabIndex` and clamp logic.
   - Reset to `initialTabIndex` on menu open (or configurable `resetOnOpen`).
2. Move listener wiring to `OnEnable`/`OnDisable` (or guard duplicate registration) so tab callbacks stay stable across re-instantiation.
3. Expose direct tab selection API (`SelectTab(int index)`) and keep `CycleTab(int direction)`.
4. Ensure label/panel state is always synchronized after open, resume, and tab change.

### Acceptance Criteria

1. Inventory always opens on expected default tab.
2. Prev/next tab controls never desync label vs panel visibility.
3. No duplicate tab change invocation after repeated open/close cycles.

---

## Phase 3 — Normalize Multiple Close Buttons

### Targets
- `Assets/Scripts/Samples/SampleMenuView.cs`
- `Assets/Scripts/Samples/PauseMenuView.cs` (if needed)
- `Assets/Scripts/Samples/InventoryMenuView.cs` (if needed)
- `Assets/Editor/SampleSceneSetupTool.cs`

### Change Plan

1. Support more than one close/back button per window using an explicit serialized array (for example `closeButtons[]`) while retaining legacy single `backButton` fields.
2. Update setup binding to apply close action (`PopBack` or modal-specific close action) to all configured close/back buttons.
3. In setup binding, always clear existing listeners/invokers before rebinding to prevent duplicate calls.
4. Add fallback handling for missing arrays so old prefabs still receive at least one bound close button.

### Acceptance Criteria

1. Every visible close/back/X button in each sample popup closes consistently.
2. No button triggers duplicate close calls.
3. Re-running setup remains idempotent (same behavior after multiple runs).

---

## Phase 4 — Setup Tool Consistency Pass

### Target
- `Assets/Editor/SampleSceneSetupTool.cs`

### Change Plan

1. Ensure setup wires:
   - Pause/open/back behavior aligned with router fixes.
   - Tab defaults and controls.
   - All close button arrays.
2. Keep the setup pass destructive/idempotent by clearing listeners before assigning new persistent listeners.
3. Keep missing-script cleanup and Input System event module validation.

### Acceptance Criteria

1. Running setup in a clean scene produces working behavior without manual inspector edits.
2. Running setup repeatedly does not multiply listeners or components.

---

## Verification Checklist

After implementation:

1. Run `Tools > YetAnotherMenuManager > Setup Gameplay HUD Sample Scene`.
2. Enter play mode and verify:
   - `ESC` opens pause on first press at startup.
   - `ESC` closes pause and pops nested modals/pages correctly.
   - Inventory tabs open with expected default and cycle reliably.
   - Every close/back/X button on sample popups works once and only once.
3. Re-run setup tool and repeat checks to confirm idempotence.
4. Confirm no missing scripts in generated runtime root.

---

## Definition of Done

1. Startup `ESC` behavior is fixed with no first-use dead path.
2. Inventory tabs are deterministic, resettable, and designer-controllable.
3. Popup close behavior is unified across all exposed close/back controls.
4. Setup tool regenerates the scene with stable, duplicate-free bindings.
5. No changes made to core services architecture during this refinement pass.
