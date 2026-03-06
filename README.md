# Yet Another Menu Manager

Overview! Control the menus!

`MenuController` is the single runtime owner of the menu stack. It:

- Maintains an ordered stack of open `UIMenuComponent` screens
- Routes `Open`, `Close`, `Pause`, `Resume` lifecycle calls to those screens
- Groups related screens under `MenuGroupDefinition` ScriptableObjects for batch show/hide
- Signals an optional `IGameplayPauseBridge` whenever the pause state changes
- Registers itself as an `IService` so any code can reach it via `Services.Get<MenuController>()`

---

## Scene Setup

### 1. Add MenuController to the scene

Place `MenuController` on a persistent GameObject (e.g. a `MenuManager` object in your scene root or under `DontDestroyOnLoad`).

```
MenuManager (GameObject)
  ├── MenuController        ← the controller
  ├── MenuInputRouter       ← optional: cancel/pause key routing
  └── TimeScalePauseBridge  ← optional: wires pause to Time.timeScale
```

`MenuController.OnEnable` calls `Services.TryAdd(this)` automatically — no installer step needed.

### 2. Create MenuGroupDefinition assets

For each logical group of screens, create a `MenuGroupDefinition` ScriptableObject:

`Assets > Create > UI > Menu Group`

Give it a unique `Group Id` (e.g. `"main-menu"`, `"pause"`, `"inventory"`). The `Display Name` is human-readable only.

### 3. Assign entries in the inspector

On `MenuController`, expand **Menu Group Entries** and add one entry per group:

| Field                | What to set                                                       |
| -------------------- | ----------------------------------------------------------------- |
| `group`              | the `MenuGroupDefinition` SO                                      |
| `menus`              | list of `UIMenuComponent` scene references in this group          |
| `hideAllOtherGroups` | pause everything else on the stack when this group opens          |
| `pauseWhenOpen`      | this group's presence contributes to `IsPaused`                   |
| `isModal`            | marks screens for `CloseAllModals()` targeting                    |
| `usesUIInput`        | contributes to `AnyUIInputMenuOpen` (drives input mode switching) |
| `openOnStartup`      | auto-show the group on `Start()`                                  |

---

## Creating Menu Screens

Subclass `UIMenuComponent`:

```csharp
using Core.UI;
using UnityEngine.UI;

public class PauseMenuView : UIMenuComponent
{
    [Header("Buttons")]
    public Button resumeButton;

    public override void Open(Dictionary<string, object> args = null)
    {
        base.Open(args); // activates GameObject, sets initial selection
        resumeButton.onClick.AddListener(OnResumeClicked);
    }

    public override void Close()
    {
        resumeButton.onClick.RemoveListener(OnResumeClicked);
        base.Close(); // deactivates GameObject
    }

    private void OnResumeClicked()
    {
        Services.Get<MenuController>().PopBack();
    }
}
```

Attach the component to a **deactivated** UI panel GameObject. `Open()` activates it; `Close()` deactivates it.

### UIMenuComponent lifecycle

```
Open(args)   → gameObject.SetActive(true)  + SetInitialSelection()
Close()      → ClearSelectionIfOwned()     + gameObject.SetActive(false)
Pause()      → ClearSelectionIfOwned()     + gameObject.SetActive(false)
Resume()     → gameObject.SetActive(true)  + SetInitialSelection()
```

`Pause` and `Close` both deactivate the screen; the difference is semantic — `Pause` means another menu was pushed on top and this one may `Resume` later. Override each independently if you need different behavior (e.g. keep a HUD visible while paused but hidden when closed).

### Passing parameters

```csharp
// show with args
menuController.Show(inventoryMenu, new Dictionary<string, object> { ["tab"] = "weapons" });

// receive in the subclass
public override void Open(Dictionary<string, object> args = null)
{
    base.Open(args);
    if (args != null && args.TryGetValue("tab", out var tab))
        SwitchTab((string)tab);
}
```

### Selection tracking

`UIMenuComponent.Update()` runs a maintenance loop: if `EventSystem.currentSelectedGameObject` goes null while the screen is active, it restores `lastSelected` (falling back to `defaultSelected`). Assign `defaultSelected` in the inspector to the first button. Set `maintainSelection = false` to disable.

---

## Showing and Hiding Menus

All public methods are on `MenuController`:

```csharp
var mc = Services.Get<MenuController>();

mc.Show(menu);                  // push onto stack
mc.Show(menu, args);            // push with parameters
mc.Hide(menu);                  // remove from stack (stack surgery if not top)
mc.Toggle(menu);                // show if closed, hide if open
mc.PopBack();                   // close top menu, resume one below
mc.CloseAll();                  // close every open menu
mc.CloseAllModals();            // pop top menus while IsTopMenuModal() is true
```

### Group operations

```csharp
mc.ShowMenuGroup(group);             // push all menus in the group
mc.ShowMenuGroup("pause");           // same, by string ID
mc.HideMenuGroup(group);             // remove all menus in the group
```

### Querying state

```csharp
mc.AnyMenuOpen          // true if stack is non-empty
mc.OpenMenuCount        // stack depth
mc.AnyUIInputMenuOpen   // true if any open menu has usesUIInput = true
mc.IsPaused             // true if any open menu has pauseWhenOpen = true
mc.IsMenuOpen(menu)     // true if menu is anywhere in the stack
mc.IsTopMenu(menu)      // true if menu is at the top of the stack
mc.IsTopMenuModal()     // true if top menu's group has isModal = true
```

### Typed lookup

```csharp
if (mc.TryGetMenuFromGroup<InventoryMenuView>(inventoryGroup, out var inv))
    inv.HighlightSlot(3);
```

---

## Stack Behavior

The stack follows a Pause/Resume contract:

```
Initial:   []

Show(A):   [A]           — A.Open()
Show(B):   [A, B]        — A.Pause(), B.Open()
Show(C):   [A, B, C]     — B.Pause(), C.Open()
PopBack(): [A, B]        — C.Close(), B.Resume()
PopBack(): [A]           — B.Close(), A.Resume()
PopBack(): []            — A.Close()
```

`Hide(B)` with C on top uses **stack surgery**: C is popped and closed, B is removed, C is re-pushed and opened. Use it for non-linear dismissal (e.g. closing a notification that is not on top).

`CloseAll()` closes every menu from top to bottom without resume calls.

### OnMenuStateChanged

Subscribe to receive a `bool` whenever the stack changes (true = any `usesUIInput` menu is open):

```csharp
menuController.OnMenuStateChanged += hasUIMenus =>
{
    inputManager.SwitchToUIMode(hasUIMenus);
};
```

---

## Async Transitions (UniTask + LeanTween)

Override `OpenAsync` and/or `CloseAsync` in your subclass to animate:

```csharp
using Cysharp.Threading.Tasks;
using Core.UI.Runtime;
using System.Threading;

public class FadeMenuView : UIMenuComponent
{
    [SerializeField] private CanvasGroup canvasGroup;

    public override async UniTask OpenAsync(MenuOpenContext ctx, CancellationToken ct)
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        await LeanTween.alphaCanvas(canvasGroup, 1f, 0.2f)
            .setEase(LeanTweenType.easeOutQuad)
            .ToUniTask(cancellationToken: ct);
        SetInitialSelection();
    }

    public override async UniTask CloseAsync(MenuCloseContext ctx, CancellationToken ct)
    {
        await LeanTween.alphaCanvas(canvasGroup, 0f, 0.15f)
            .setEase(LeanTweenType.easeInQuad)
            .ToUniTask(cancellationToken: ct);
        gameObject.SetActive(false);
    }
}
```

The base implementations call the sync methods and return `UniTask.CompletedTask` — safe to leave unoverridden if no animation is needed.

`InstantShow(args)` and `InstantHide()` always call the sync path regardless of overrides — use them for immediate resets (e.g. loading screen cut).

---

## Pause Bridge

Wire a gameplay pause signal by implementing `IGameplayPauseBridge` and assigning the component to `MenuController.gameplayPauseBridge`:

**Ready-made — `TimeScalePauseBridge`** (`Runtime/MenuExternalIntegration.cs`):
Sets `Time.timeScale` to `pausedTimeScale` (default 0) when any `pauseWhenOpen` menu is open.

```
MenuManager (GameObject)
  ├── MenuController             ← gameplayPauseBridge → TimeScalePauseBridge
  └── TimeScalePauseBridge
```

**Custom — implement the interface directly:**

```csharp
public class FusionPauseBridge : MonoBehaviour, IGameplayPauseBridge
{
    public void SetGameplayPaused(bool paused)
    {
        // suspend Fusion simulation or disable player input
    }
}
```

---

## Input Routing

### MenuInputRouter

`Assets/Plugins/YetAnotherMenuManager/UI/MenuInputRouter.cs` — reads `GameInputManager` each frame and maps pause/cancel inputs to stack operations:

| Input                     | Behaviour                                                                                                                  |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Pause pressed             | If pause menu is not open: `Show(pauseMenu)`. If it is open but not on top: pops back to it. If it is on top: `PopBack()`. |
| Cancel pressed (no pause) | If stack is empty or only `rootGameplayMenu` is open: treats as pause. Otherwise: `PopBack()`.                             |

Assign `pauseMenu` and `rootGameplayMenu` in the inspector. `MenuInputRouter` requires `MenuController` on the same GameObject.

### MenuInputModeBridge

`Runtime/MenuExternalIntegration.cs` — subscribes to `MenuController.OnMenuStateChanged` and calls `inputManager.SwitchToUIMode()` / `SwitchToGameMode()` automatically. Assign alongside `MenuController`.

### MenuActionInvoker

`Runtime/MenuExternalIntegration.cs` — inspector-configurable component that exposes one `MenuActionType` (Show, Hide, Toggle, ShowGroup, etc.) and a target menu/group. Wire its `InvokeAction()` to a UnityEvent (e.g. a button's `onClick`) to avoid code in simple UI wiring cases.
