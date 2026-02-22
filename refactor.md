# Refactor Plan v4: MenuController-Only (Destructive First Pass)

## Context

The project currently has two overlapping menu systems:

|              | `MenuController`                    | Runtime subsystem                                          |
| ------------ | ----------------------------------- | ---------------------------------------------------------- |
| Main type    | `MenuController`                    | `MenuRuntimeController` (+ gateway/adapter)                |
| Data model   | Direct `UIMenuComponent` references | `MenuDefinition`/`MenuCatalog` indirection                 |
| Flow helpers | Stack/group operations              | Additional stack/conditional system (`MenuStackNavigator`) |
| Current size | 376 lines                           | 992 + 518 + related contracts/loaders                      |

This pass intentionally optimizes for deletion and simplification, not compatibility.

---

## Assumptions

1. Breaking current scene/prefab wiring is acceptable.
2. We will regenerate sample scenes from `SampleSceneSetupTool`.
3. No migration shims or dual-controller bridge period is required.
4. Do not change Services, EventManager, BaseSceneServiceInstaller, and GlobalServicesInstaller. 

## Execution Order

1. Delete runtime subsystem first.
2. Extend `MenuController` to fully cover required behavior.
3. Rewire integrations/tooling to `MenuController`.
4. Trim contracts/services/helpers.
5. Validate only regenerated sample scenes.

---

## Phase 0 — Baseline Safety

Before edits:

1. Capture compile baseline.
2. Capture current line counts for targeted files.
3. Keep a short regression checklist for regenerated HUD sample:
   - HUD visible on start.
   - Pause opens/closes repeatedly.
   - ESC/back pops modal first, then page.
   - Lobby icons animate in/out and respect stack rules.
   - Inventory tab switching works.

---

## Phase 1 — Hard Deletions

Delete these files/classes immediately:

| Action      | File                                                           | Current lines |
| ----------- | -------------------------------------------------------------- | ------------- |
| Delete file | `Assets/Scripts/Core/UI/Runtime/MenuRuntimeController.cs`      | 992           |
| Delete file | `Assets/Scripts/Core/UI/Runtime/MenuStackNavigator.cs`         | 518           |
| Delete file | `Assets/Scripts/Core/UI/Runtime/MenuTransitionOrchestrator.cs` | 203           |
| Delete file | `Assets/Scripts/Core/UI/Runtime/MenuDefinitions.cs`            | 268           |
| Delete file | `Assets/Scripts/Core/UI/Runtime/MenuLoaders.cs`                | 87            |
| Delete file | `Assets/Scripts/Core/Services/EventManager.cs`                 | 20            |

Notes:

- `AddressablesMenuLoader` is removed as part of `MenuLoaders.cs` deletion.
- Compile breakage after this phase is expected.

---

## Phase 2 — Make MenuController Fully Canonical

Expand `MenuController` so no runtime subsystem is needed.

Required surface:

1. `Show`, `Hide`, `Toggle` for direct `UIMenuComponent` targets.
2. `ShowGroup`, `HideGroup` (existing, keep/refine).
3. `PopBack`, `CloseAll`, `SetPaused`.
4. Optional async wrappers (`ShowAsync`, `HideAsync`) only if necessary for animated menus.

Rules:

1. Keep one stack model only.
2. No duplicate “runtime vs legacy” paths.
3. Keep API inspector-friendly for designers.

---

## Phase 3 — Rewire External Integration to MenuController

Update `MenuExternalIntegration.cs`:

1. `MenuActionInvoker` resolves `MenuController` directly.
2. Remove routing to deleted runtime types (`MenuRuntimeController`, `IMenuCommands`, `MenuStackNavigator`).
3. Shrink `MenuActionType` to only actions actually used post-refactor.
4. Keep `MenuInputModeBridge` but subscribe to `MenuController` state events.

---

## Phase 4 — Rebuild Setup Tool Around Direct References

Refactor `Assets/Editor/SampleSceneSetupTool.cs`:

1. Stop creating/wiring runtime-definition assets/components.
2. Build HUD prefabs and wire direct component references only.
3. Configure `MenuController` entries and action invokers directly.
4. Remove any USN/runtime-legacy setup branches.
5. Keep missing-script cleanup in generated scene.

Goal: one setup path, minimal architecture, deterministic regenerated scene.

---

## Phase 5 — Localize Lobby Icon Behavior

Without `MenuConditionalVisibilityController`, keep lobby behavior local:

1. Keep animation and edge behavior in `LobbyIconsHudView` (or tiny coordinator script).
2. Drive state from `MenuController` stack/menu-state events.
3. Preserve required UX:
   - icons on edges
   - animate in and out
   - hide/collapse when menu stack blocks interaction



---

## Line Count Targets

| File                                                           | Before   | After (target) | Delta            |
| -------------------------------------------------------------- | --------:| --------------:| ----------------:|
| `Assets/Scripts/Core/UI/Runtime/MenuRuntimeController.cs`      | 992      | 0              | -992             |
| `Assets/Scripts/Core/UI/Runtime/MenuStackNavigator.cs`         | 518      | 0              | -518             |
| `Assets/Scripts/Core/UI/Runtime/MenuTransitionOrchestrator.cs` | 203      | 0              | -203             |
| `Assets/Scripts/Core/UI/Runtime/MenuDefinitions.cs`            | 268      | 0              | -268             |
| `Assets/Scripts/Core/UI/Runtime/MenuLoaders.cs`                | 87       | 0              | -87              |
| `Assets/Scripts/Core/Services/EventManager.cs`                 | 20       | 0              | -20              |
| `Assets/Scripts/Core/UI/MenuController.cs`                     | 376      | ~520           | +144             |
| `Assets/Scripts/Core/UI/Runtime/MenuExternalIntegration.cs`    | 252      | ~120           | -132             |
| `Assets/Scripts/Core/UI/Runtime/MenuRuntimeContracts.cs`       | 190      | ~35            | -155             |
| `Assets/Scripts/Samples/LobbyIconsHudView.cs`                  | 249      | ~90            | -159             |
| `Assets/Scripts/Core/Services.cs`                              | 529      | ~280           | -249             |
| `Assets/Scripts/Core/BaseSceneServicesInstaller.cs`            | 56       | ~28            | -28              |
| `Assets/Editor/SampleSceneSetupTool.cs`                        | 1444     | ~650           | -794             |
| **TOTAL (tracked set)**                                        | **5184** | **~1723**      | **-3461 (~67%)** |

---

## What Is Preserved

1. Designer-facing direct wiring via `MenuController`.
2. Stack/group navigation semantics.
3. UI input mode switching behavior.
4. Pause/open-close behavior expected by HUD sample.
5. Lobby icon edge/in-out UX requirements.

---

## What Is Removed

1. Entire runtime-definition subsystem (`MenuRuntimeController`, definitions/catalog/loaders/orchestrator).
2. Runtime stack/conditional layer (`MenuStackNavigator` + nested controller).
3. Gateway/adapter indirection paths.
4. Addressables loader stub.
5. Empty event manager stub.

---

## Verification

After all phases:

1. Zero compile errors.
2. Run `Tools > YetAnotherMenuManager > Setup Gameplay HUD Sample Scene` in a clean scene.
3. Enter Play Mode and verify:
   - HUD starts visible.
   - Pause opens/closes repeatedly without lockup.
   - ESC/back order is correct (modal then page).
   - Lobby icons animate in/out and stay on edges.
   - Inventory tab buttons cycle correctly.
4. Confirm no missing-script components in generated scene.

---

## Definition of Done

1. `MenuController` is the only runtime menu controller.
2. Runtime subsystem files are deleted.
3. `AddressablesMenuLoader` is removed.
4. Sample scene setup regenerates a working HUD flow from scratch.
5. Duplicate architecture is eliminated with major net code reduction.
