# Menu Controller Feature Plan

## Purpose
This document is the planning baseline for menu-system features in YetAnotherMenuManager.

## Current System Map

### Core Runtime Components
1. `MenuController` (`Assets/Scripts/Core/UI/MenuController.cs`)
2. `UIMenuComponent` (`Assets/Scripts/Core/UI/UIMenuComponent.cs`)
3. `MenuGroupDefinition` (`Assets/Scripts/Core/UI/MenuGroupDefinition.cs`)
4. `MenuInputRouter` (`Assets/Scripts/Core/UI/MenuInputRouter.cs`)

### Data Model
1. Menu groups are designer-authored `ScriptableObject` assets (`MenuGroupDefinition`) with:
- `groupId` (string key)
- `displayName` (UI/debug label)
2. `MenuController` stores serialized `menuGroupEntries` where each entry defines:
- group asset reference
- list of menus in that group
- `hideAllOtherGroups`
- `pauseWhenOpen`
- `isModal`
- `usesUIInput`
- `openOnStartup`

### Initialization and Registration
1. `MenuController` is a singleton and registers itself with `Services` on `OnEnable`.
2. In `StartService`, the controller builds lookup caches:
- group -> menus
- menu -> group entry
- group -> group entry
- `groupId` -> group
3. In `Start`, any entries marked `openOnStartup` are opened.

### Stack and Navigation Behavior
1. Menus are managed as a stack with paired parameter stack (`Dictionary<string, object>`).
2. `PushMenu`:
- pauses current top menu
- pushes new menu + params
- opens new menu
3. `PopMenu`:
- closes top menu
- resumes next menu
- restores a modal snapshot when needed
4. `ReplaceMenu` closes all menus and opens one target menu.
5. `RemoveMenu` removes a menu from anywhere in stack, then reopens menus above it.

### Group Operations
1. `ShowMenuGroup(MenuGroupDefinition, args, modal)` opens all menus in a group.
2. `ShowMenuGroup(string groupId, args, modal)` resolves by ID and opens group.
3. `HideMenuGroup(MenuGroupDefinition)` closes all menus in a group.
4. `OpenMenu(MenuGroupDefinition)` opens first menu found in a group.
5. `OpenMenu(string groupId)` resolves and opens by ID.
6. `TryGetMenuFromGroup<T>` supports typed lookup by group reference or string ID.

### Modal and Pause Semantics
1. Groups marked `hideAllOtherGroups` pause currently open menus before opening.
2. Modal opens (`modal = true`) capture stack snapshot, close all, then open modal group.
3. After modal is popped and stack is empty, previous snapshot is restored.
4. `IsPaused` returns true if any open menu belongs to a `pauseWhenOpen` entry.
5. `IsTopMenuModal` blocks cancel/back behavior for modal top menus.

### Input and UI Mode Integration
1. `MenuController` publishes `OnMenuStateChanged(bool hasUIMenus)` based on `usesUIInput`.
2. `MenuInputRouter` listens for menu state changes and switches input modes:
- UI mode when any UI-input menu is open
- Game mode when none are open
3. `MenuInputRouter` handles keyboard shortcuts:
- `Esc` pops menu unless top menu is modal
- `P` opens configured pause group as modal
4. `MenuInputRouter` uses serialized menu-group assets (`pauseMenuGroup`, `playerCommandMenuGroup`).

### Selection Behavior in Menus
1. `UIMenuComponent.Open/Resume` calls `SetInitialSelection`.
2. If `defaultSelected` is missing, first active and interactable `Selectable` child is used.
3. Selection is maintained every frame when `maintainSelection` is true.

## Known Gaps and Planning Targets
1. No centralized validation for duplicate `groupId` values.
2. No editor tooling to auto-audit unassigned group references.
3. No explicit transition/animation orchestration at controller layer.
4. No built-in menu history analytics or structured telemetry.
5. No test suite currently covering stack behavior and modal snapshot restoration.

## Next Planning Sections
1. Proposed features
2. Priority and scope
3. Implementation design notes
4. Acceptance criteria
5. Test plan

## Configuration Alternatives (Reduce MenuController Prefab Setup)

### Problem
The current `menuGroupEntries` list in `MenuController` requires centralized prefab setup. This creates manual wiring overhead and higher risk of stale or missing references.

### Option 1: Self-Registering Menus (Recommended)
Each menu prefab/component gets a `MenuRegistration` component with:
- `MenuGroupDefinition group`
- `pauseWhenOpen`
- `isModal`
- `usesUIInput`
- `openOnStartup`
- `hideAllOtherGroups` (group-level value handled by first registration or group settings asset)

`MenuController` discovers all registrations via `GetComponentsInChildren<MenuRegistration>(true)` (or scene scope), then builds lookups automatically.

Pros:
- Designers configure each menu where it lives.
- No big central list to maintain.
- Easy to add/remove menus without touching controller prefab.

Cons:
- Requires validation for conflicting group-level flags.
- Discovery order must be deterministic if order matters.

### Option 2: Group-Centric ScriptableObject Assets
Create `MenuGroupConfig` assets containing:
- group reference
- group behavior flags
- menu references (or menu prefab references)

`MenuController` references only a list of group config assets.

Pros:
- Clear ownership by group.
- Reusable across scenes/prefabs.
- Good for designer workflows with shared UI sets.

Cons:
- Still centralized setup, just moved to assets.
- Asset references can drift from scene instances if not validated.

### Option 3: Hybrid Auto-Build + Manual Override
Keep `menuGroupEntries`, but add editor tooling:
- `Rebuild From Children` button
- Validation warnings for nulls/duplicates
- Optional lock/manual override entries

Pros:
- Minimal runtime refactor.
- Fastest migration with low risk.
- Keeps existing serialized structure intact.

Cons:
- Still a generated list that can become stale.
- Depends on designers using tooling consistently.

### Option 4: Scene Installer Pattern
Create `MenuInstaller` components near each menu/module that register on boot:
- `MenuController.Register(MenuRegistration data)`

Pros:
- Modular and scalable for large scenes/features.
- Strong separation by feature teams.

Cons:
- Slightly more code complexity.
- Registration timing/order needs guardrails.

### Recommendation
Start with **Option 1 (Self-Registering Menus)** and add lightweight editor validation:
1. Implement `MenuRegistration` and runtime auto-discovery.
2. Add duplicate/invalid configuration warnings.
3. Keep temporary compatibility with existing `menuGroupEntries` during migration.
4. Remove legacy list once all menus use registration.

## Scenario Architecture: Gameplay HUD + Pause Exceptions + Runtime Loading

### Goals
1. Support many simultaneous one-screen menus (health, minimap, quest tracker, etc.).
2. On pause, hide most menus but keep specific exceptions visible.
3. Load menus on demand so they are not all pre-instantiated.

### Proposed Model

### 1) Split Concepts: Definition vs Instance
Use `MenuDefinition` (ScriptableObject) as source of truth:
- `group` / `groupId`
- `layer` (HUD, Overlay, Modal, Debug, etc.)
- `prefab` or `addressKey`
- `loadMode` (`Preload`, `Lazy`, `OnDemand`)
- `unloadPolicy` (`KeepLoaded`, `UnloadOnClose`, `UnloadAfterDelay`)
- `pauseVisibility` (`HideOnPause`, `ShowOnPause`, `IgnorePause`)
- `usesUIInput`, `pauseGameWhenOpen`, `isModal`

Runtime creates `MenuInstance` records:
- definition reference
- instantiated component
- loaded/visible state
- last args

### 2) Layer-Based Orchestration
Replace single stack-only mental model with layers:
1. Persistent layers (HUD) can show many menus at once.
2. Overlay/Modal layers behave stack-like.
3. Visibility is resolved per layer + policy, not only by open order.

This allows:
- health + minimap + crosshair active together
- pause menu on top without deleting HUD registrations

### 3) Pause Visibility Rules (Hide With Exceptions)
When pause opens:
1. Controller enters `Paused` UI context.
2. For each visible menu:
- if `pauseVisibility == HideOnPause`, hide
- if `pauseVisibility == ShowOnPause`, keep visible
- if `IgnorePause`, no change
3. Cache hidden set and restore exactly that set on resume.

This avoids brittle hardcoded exception lists in code.

### 4) Runtime Loading Pipeline
Introduce `IMenuLoader`:
- `LoadAsync(MenuDefinition def)`
- `Unload(MenuDefinition def)` / `Release(instance)`

Implementation options:
1. Addressables-based loader (recommended for scale)
2. Resources/prefab fallback for small projects

Open flow:
1. `Show(def)` requested
2. If not loaded, async load + instantiate under layer root
3. Register with orchestrator
4. Apply visibility context (paused/unpaused)
5. Open with args

### 5) API Shape
Use definition-first API:
1. `Show(MenuDefinition def, Dictionary<string, object> args = null)`
2. `Hide(MenuDefinition def)`
3. `Toggle(MenuDefinition def)`
4. `ShowGroup(MenuGroupDefinition group)`
5. `SetPaused(bool paused)`
6. `Preload(IEnumerable<MenuDefinition>)`

### 6) Authoring Workflow for Designers
1. Create `MenuDefinition` assets per menu.
2. Assign pause visibility policy per menu.
3. Assign load mode per menu.
4. (Optional) Create `MenuSetDefinition` assets:
- `GameplayHUDSet` (health, minimap, quest tracker)
- `PauseSet` (pause root, pause background)
5. Game state systems request sets rather than manual menu-by-menu wiring.

### 7) Migration Path from Current System
1. Replace current `MenuController` with `MenuRuntimeController` + `MenuTransitionOrchestrator`.
2. Migrate existing menu prefabs into `MenuDefinition` assets.
3. Add loader layer (`IMenuLoader`) and route all `Show/Hide` through async API.
4. Move high-frequency HUD menus first.
5. Move pause menu and validate hide/exception behavior and pause gates.
6. Remove legacy group-entry prefab setup and enum-driven flows.

### 8) Risks and Guardrails
1. Async race conditions (open then immediate hide): require request versioning/cancellation.
2. Duplicate definitions in scene: validate and warn in editor/runtime.
3. Memory churn from frequent load/unload: use unload delay and pooling for common menus.
4. Missing address keys/prefabs: fail fast with clear logs.

## Async Open/Close and Transition Orchestration

### Requirements
1. Menu open/close must support async animation lifecycles.
2. Some transitions should overlap (new menu can open while previous closes).
3. Some transitions must block progression until complete.
4. Example hard requirement: unpause gameplay only after pause menu close animation is fully finished.

### Lifecycle Contract
Replace fire-and-forget `Open/Close` with async operations on menu instances:
1. `Task OpenAsync(MenuOpenContext ctx, CancellationToken ct)`
2. `Task CloseAsync(MenuCloseContext ctx, CancellationToken ct)`
3. Optional fast path: `InstantShow()` / `InstantHide()` for forced state correction.

Each menu reports completion only when animation and interaction-state changes are finished.

### Transition Policies
Use explicit policy per transition request:
1. `FireAndForget`: trigger and continue immediately.
2. `WaitForStart`: continue once target menu is visible/interactable.
3. `WaitForComplete`: continue only after full open/close completes.
4. `WaitForBoth`: open request waits for prerequisite close + target open completion.

Policy can be configured by:
1. Per-menu default in `MenuDefinition`.
2. Per-call override from gameplay flow/state machine.

### Orchestration Graph (Not Just Stack Calls)
Introduce `MenuTransitionOrchestrator`:
1. Builds transition plan for requested state change.
2. Starts close/open operations in dependency order.
3. Applies overlap rules:
- `allowOpenDuringClose` true: start opens once closes have reached `WaitForStart` condition.
- false: start opens only after required closes complete.
4. Returns a completion task representing the requested policy.

### Blocking Points (Gameplay Integration)
Add explicit gate options for game state transitions:
1. `PauseEnterGate`: when to freeze gameplay (`Immediately`, `OnPauseMenuVisible`, `OnPauseMenuOpened`).
2. `PauseExitGate`: when to unfreeze gameplay (`Immediately`, `OnPauseMenuCloseStarted`, `OnPauseMenuClosed`).

For your requirement:
1. Configure `PauseExitGate = OnPauseMenuClosed`.
2. Unpause executes only after `CloseAsync` completion for pause root menu.

### Menu Definition Additions
Add transition metadata to `MenuDefinition`:
1. `openMode`: `Parallel`, `AfterPrerequisites`.
2. `closeMode`: `Parallel`, `BeforeNext`.
3. `openBlocking`: `None`, `UntilVisible`, `UntilComplete`.
4. `closeBlocking`: `None`, `UntilHidden`, `UntilComplete`.
5. `allowInputDuringTransition` (bool).
6. `transitionTimeoutMs` (safety fallback).

### Queueing, Cancellation, and Reentrancy
1. One active transition plan per layer/context.
2. New request can:
- enqueue
- coalesce/replace pending request
- interrupt current transition (if permitted)
3. Every operation has cancellation token; interrupted animations must end in deterministic state.
4. Use request/version IDs to ignore stale completion callbacks.

### Suggested Implementation Shape
1. `IAnimatedMenu`
- `Task OpenAsync(...)`
- `Task CloseAsync(...)`
- `bool IsTransitioning`
2. `MenuRuntimeController`
- owns loaded instances + visibility targets
- talks to loader
3. `MenuTransitionOrchestrator`
- computes dependencies
- executes transition plan
- emits completion task/events
4. `IGameplayPauseBridge`
- receives pause state intents
- applies configured pause enter/exit gates

### Example Flows

#### Pause Open (Allow Some Overlap)
1. Request `SetPaused(true)`.
2. Hide `HideOnPause` HUD menus (non-blocking close).
3. Open pause menu (blocking until visible/open complete based on policy).
4. Freeze gameplay at configured gate.

#### Pause Close (Strict Gate for Unpause)
1. Request `SetPaused(false)`.
2. Start pause menu close.
3. Wait for `CloseAsync` completion (`PauseExitGate = OnPauseMenuClosed`).
4. Unfreeze gameplay.
5. Restore previously hidden HUD menus (parallel open allowed).

### Validation Rules
1. Warn when `PauseExitGate` requires completion but pause menu has no close animation/handler.
2. Warn on circular transition prerequisites.
3. Warn when transition timeout is zero for blocking transitions.

## External Benchmark (Asset Store + Open Source)

### Sources Snapshot
Verified on February 20, 2026.

### Asset Store Projects
1. **UI Graph – A Menu System For Unity** (`2.0.3`, latest release date Oct 2, 2019).
2. **Simple UI Window Manager** (`1.0`, latest release date Sep 6, 2024).

### Open Source Projects
1. **UnityScreenNavigator** (Haruma-K).
2. **NavStack** (AnnulusGames).
3. **Blitzy unity-ui-manager**.

### Feature Comparison
Legend: `Yes` = explicitly documented, `Partial` = available but with constraints, `No` = not documented.

| Capability | Current YAMM | Planned YAMM | UI Graph (Asset Store) | Simple UI Window Manager (Asset Store) | UnityScreenNavigator (OSS) | NavStack (OSS) | Blitzy (OSS) |
|---|---|---|---|---|---|---|---|
| Multi-menu HUD + modal overlays | Partial | Yes | Partial | Partial | Yes | Yes | Partial |
| Visual authoring flow graph | No | No | Yes | No | No | No | No |
| Async open/close lifecycle API | No | Yes | Partial | No | Yes | Yes | No |
| Transition overlap policy controls | No | Yes | Partial | No | Partial | Partial | No |
| Per-menu pause visibility exceptions | No | Yes | No | No | No | No | No |
| Pause unfreeze gate tied to close completion | No | Yes | No | No | Partial | Partial | No |
| Runtime lazy loading/on-demand instantiation | No | Yes | Partial | No | Yes | Partial | Yes |
| Addressables-ready loading abstraction | No | Yes | No (not documented) | No | Yes | Yes | No |
| Built-in preloading API | No | Yes | No (not documented) | No | Yes | No (not documented) | No |

### Key Insights
1. Current architecture is behind modern OSS systems on async transitions and runtime loading.
2. Planned architecture exceeds common offerings on **pause policy + gate semantics** (critical for your pause fade requirement).
3. `UnityScreenNavigator` is the closest reference implementation for mature loading and transition controls.
4. `NavStack` is a strong reference for async-first navigation contracts and reentrancy options.
5. Asset Store offerings in this niche are either older (UI Graph) or lightweight (Simple UI Window Manager) relative to your target requirements.

### Priority Implications
1. Build async lifecycle and orchestrator first (`IAnimatedMenu`, transition policy, queueing/cancellation).
2. Implement pause visibility policy and pause exit gate next.
3. Add loader abstraction with Addressables support and optional preloading.
4. Add editor validation and diagnostics after runtime parity.

## Concrete Target Architecture (v1)

### Scope and Non-Goals
This target architecture is for runtime UI/menu orchestration in gameplay scenes.

Included:
1. Multi-layer menu orchestration (`HUD`, `Overlay`, `Modal`, optional `Debug`).
2. Async open/close lifecycle with transition policy and deterministic cancellation.
3. Runtime loading (Addressables-first, Resources fallback) and preload/release policy.
4. Pause visibility policies and strict pause exit gate support.
5. Compatibility bridge to current `MenuController` calls during migration.

Not included in v1:
1. Visual graph editor tooling.
2. Full telemetry backend integration.
3. UI Toolkit support.

### Runtime Module Boundaries

#### 1) Definitions and Registry
Owns author-time data and lookup.

Types:
1. `MenuDefinition` (ScriptableObject)
2. `MenuSetDefinition` (optional aggregate list of `MenuDefinition`)
3. `MenuCatalog` (runtime registry)

Responsibilities:
1. Resolve menu/group/layer identity.
2. Validate duplicate IDs and conflicting flags at startup.
3. Expose immutable query API to runtime modules.

#### 2) Instance and Loading Layer
Owns instantiation and release.

Types:
1. `IMenuLoader`
2. `AddressablesMenuLoader`
3. `ResourcesMenuLoader`
4. `MenuInstanceRecord`

Responsibilities:
1. `LoadAsync(def, ct)` and `Release(instance)` lifecycle.
2. Maintain loaded-instance cache keyed by `MenuDefinition`.
3. Respect `loadMode` and `unloadPolicy`.

#### 3) Menu Runtime Facade
Owns high-level menu API and desired state.

Types:
1. `MenuRuntimeController`
2. `MenuRequest`
3. `MenuVisibilityState`

Responsibilities:
1. Public API (`Show`, `Hide`, `Toggle`, `ShowGroup`, `SetPaused`, `Preload`).
2. Convert API calls into normalized requests.
3. Maintain desired visible set and versioned request IDs.

#### 4) Transition Planning and Execution
Owns ordering and async guarantees.

Types:
1. `MenuTransitionOrchestrator`
2. `MenuTransitionPlan`
3. `MenuTransitionStep`
4. `TransitionPolicy`

Responsibilities:
1. Build transition DAG (prereqs, overlap rules, blocking gates).
2. Execute steps with cancellation/version checks.
3. Return completion task according to policy.

#### 5) Pause and Input Integration
Owns gameplay/UI mode gates and pause semantics.

Types:
1. `MenuPauseCoordinator`
2. `IGameplayPauseBridge`
3. `MenuInputModeBridge`

Responsibilities:
1. Apply `pauseVisibility` filtering.
2. Freeze/unfreeze gameplay at configured enter/exit gates.
3. Switch input mode based on active menus requiring UI input.

#### 6) External Trigger Integration
Owns menu requests coming from non-menu scene objects.

Types:
1. `IMenuCommands` (narrow command surface)
2. `MenuCommandGateway` (runtime implementation over `MenuRuntimeController`)
3. `MenuActionInvoker` (simple MonoBehaviour hook for scene objects)
4. `IMenuRequestContextProvider` (optional context/args provider per caller)

Responsibilities:
1. Allow world/UI/non-menu objects to request `Show/Hide/Toggle` safely.
2. Keep external callers decoupled from menu internals (`UIMenuComponent`, stack details).
3. Normalize caller metadata (source object, priority, request context) for orchestration.

### Data Contracts

#### MenuDefinition (author-time)
Required fields:
1. `id: string` (unique)
2. `group: MenuGroupDefinition`
3. `layer: MenuLayer`
4. `prefabRef/addressKey`
5. `usesUIInput: bool`
6. `isModal: bool`
7. `pauseVisibility: HideOnPause | ShowOnPause | IgnorePause`
8. `loadMode: Preload | Lazy | OnDemand`
9. `unloadPolicy: KeepLoaded | UnloadOnClose | UnloadAfterDelay`
10. `openBlocking: None | UntilVisible | UntilComplete`
11. `closeBlocking: None | UntilHidden | UntilComplete`
12. `allowOpenDuringClose: bool`
13. `transitionTimeoutMs: int`

#### MenuInstanceRecord (runtime)
Fields:
1. `definition`
2. `instance: UIMenuComponent`
3. `isLoaded`
4. `isVisible`
5. `isTransitioning`
6. `lastArgs`
7. `lastRequestVersion`

#### MenuRequest
Fields:
1. `requestId`
2. `requestVersion`
3. `kind: Show | Hide | Toggle | ShowGroup | SetPaused`
4. `target menus/groups`
5. `policy: FireAndForget | WaitForStart | WaitForComplete | WaitForBoth`
6. `ct`

### Layer and Navigation Semantics

#### Layer rules
1. `HUD`: many visible simultaneously, no implicit stack.
2. `Overlay`: stack-like by priority/order, optional multi-visible.
3. `Modal`: strict top-of-stack ownership; blocks back/cancel beneath top modal.
4. `Debug`: non-blocking optional layer.

#### Navigation operations
1. `Show(def, args)`:
- ensure loaded
- plan close/open prerequisites
- run transition plan
2. `Hide(def)`:
- close with policy
- apply unload policy
3. `ShowGroup(group)`:
- expand to definitions and execute one batch request
4. `SetPaused(bool)`:
- run pause visibility transform + pause gate sequence

### Transition Execution Rules

#### Determinism and reentrancy
1. One active orchestration lane per layer.
2. Global request version increments on each accepted request.
3. Completion callbacks check version before mutating state.
4. Interrupted transitions force final visual correction (`InstantShow/InstantHide`) if needed.

#### Cancellation behavior
1. If a new higher-priority request arrives, current plan may be interrupted.
2. Interrupted step outcomes:
- `OpenAsync` interrupted -> hidden unless explicitly retained by new plan.
- `CloseAsync` interrupted -> continue close or force hidden based on new desired state.
3. Always end in a state consistent with current desired visible set.

#### Timeout behavior
1. Blocking steps use `transitionTimeoutMs`.
2. On timeout:
- log structured warning
- force deterministic fallback state
- continue orchestration

### Public API (Target)
`MenuRuntimeController` surface:
1. `Task Show(MenuDefinition def, Dictionary<string, object> args = null, TransitionPolicy? policy = null, CancellationToken ct = default)`
2. `Task Hide(MenuDefinition def, TransitionPolicy? policy = null, CancellationToken ct = default)`
3. `Task Toggle(MenuDefinition def, Dictionary<string, object> args = null, TransitionPolicy? policy = null, CancellationToken ct = default)`
4. `Task ShowGroup(MenuGroupDefinition group, Dictionary<string, object> args = null, TransitionPolicy? policy = null, CancellationToken ct = default)`
5. `Task SetPaused(bool paused, CancellationToken ct = default)`
6. `Task Preload(IEnumerable<MenuDefinition> defs, CancellationToken ct = default)`
7. `bool TryGetInstance(MenuDefinition def, out UIMenuComponent instance)`

`IMenuCommands` surface (for non-menu callers):
1. `Task Show(MenuDefinition def, Dictionary<string, object> args = null, CancellationToken ct = default)`
2. `Task Hide(MenuDefinition def, CancellationToken ct = default)`
3. `Task Toggle(MenuDefinition def, Dictionary<string, object> args = null, CancellationToken ct = default)`
4. `Task ShowGroup(MenuGroupDefinition group, Dictionary<string, object> args = null, CancellationToken ct = default)`
5. `Task SetPaused(bool paused, CancellationToken ct = default)`

### External Trigger Rules (Non-Menu Objects)

#### Allowed callers
1. World objects (interaction volumes, NPCs, puzzle devices).
2. HUD widgets not implemented as `UIMenuComponent`.
3. Gameplay systems/services/state machines.

#### Access pattern
1. External objects depend on `IMenuCommands` only.
2. They never call `MenuRuntimeController` internals or manipulate stacks directly.
3. They pass optional caller context (source + args) through request metadata.

#### Conflict resolution
1. Requests from external objects follow the same orchestration queue/versioning rules.
2. Caller priority can be added to request metadata when needed (`System` > `Player` > `Ambient`).
3. Stale requests from destroyed/non-active sources are ignored at execution time.

#### Simple integration component
`MenuActionInvoker` should support inspector-configurable actions:
1. target `MenuDefinition` or `MenuGroupDefinition`
2. action type (`Show/Hide/Toggle/ShowGroup/SetPaused`)
3. optional args provider (`IMenuRequestContextProvider`)
4. optional policy override

This gives designers a no-code path for non-menu objects to control menus.

### Compatibility Layer (Migration Safety)
Add `LegacyMenuControllerAdapter` to preserve current call sites while new system rolls out.

Adapter mappings:
1. `ShowMenuGroup(group, args, modal)` -> `ShowGroup(group, args, policy)`
2. `OpenMenu(groupId)` -> `Show(def)`
3. `PopMenu()` -> modal/overlay layer pop operation
4. `OnMenuStateChanged` -> `MenuInputModeBridge` events

Decommission criteria:
1. No gameplay code calls legacy `MenuController`.
2. All menus represented by `MenuDefinition`.
3. Existing test flows pass using adapter disabled.

### Validation and Startup Checks
Run at boot (development builds + editor):
1. duplicate `MenuDefinition.id`.
2. duplicate `MenuGroupDefinition.groupId`.
3. missing prefab/address refs.
4. illegal flag combos:
- `isModal` on `HUD` layer
- blocking policy with zero timeout
5. unresolved loader keys.
6. `MenuActionInvoker` missing target definition/group for configured action.
7. duplicate or conflicting invokers bound to the same trigger where exclusive behavior is required.

### Test Plan (Architecture-level)

#### Unit tests
1. Transition plan building for mixed `HUD + Modal`.
2. Cancellation/versioning correctness.
3. Pause visibility transform and hidden-set restore.
4. Timeout fallback behavior.

#### PlayMode tests
1. Pause close gate: gameplay resumes only after pause close completion.
2. Modal stack behavior with back/cancel.
3. Group show/hide with lazy-loaded menus.
4. Input mode switching from `usesUIInput`.
5. World object trigger can show/hide/toggle menus through `IMenuCommands`.
6. Destroyed trigger source does not apply stale delayed menu requests.

### Implementation Phases and Exit Criteria

#### Phase 1: Foundations
Deliver:
1. `MenuDefinition`, `MenuCatalog`, `IMenuLoader` implementations.
2. `MenuRuntimeController` minimal API (`Show/Hide` single menu).
3. `IMenuCommands` + `MenuCommandGateway`.
Exit:
1. One existing menu can be loaded/opened/closed async from definition.
2. One non-menu scene object can invoke show/hide via `IMenuCommands`.

#### Phase 2: Orchestration
Deliver:
1. `MenuTransitionOrchestrator`, policies, queueing/cancellation/versioning.
2. Layer semantics (`HUD/Overlay/Modal`).
Exit:
1. Deterministic overlapping transitions pass unit tests.

#### Phase 3: Pause and Input
Deliver:
1. `MenuPauseCoordinator`, `IGameplayPauseBridge`, `MenuInputModeBridge`.
2. Pause visibility + strict pause exit gate.
Exit:
1. Required pause flow works: unpause only after pause menu close completion.

#### Phase 4: Compatibility and Migration
Deliver:
1. `LegacyMenuControllerAdapter`.
2. Incremental migration of current groups to definitions.
Exit:
1. Legacy and new APIs both functional for targeted scenes.

#### Phase 5: Hardening
Deliver:
1. Startup validation, diagnostics, and profiling hooks.
2. PlayMode regression suite for menu navigation.
Exit:
1. Legacy controller removable without behavior regressions.
