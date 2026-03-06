using System;
using Core.Interfaces;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Managers
{
    public class GameInputManager : MonoBehaviour, IService
    {
        private const string GeneratedActionsTypeName = "InputSystem_Actions";
        private const string PlayerMoveActionPath = "Player/Move";
        private const string PlayerLookActionPath = "Player/Look";
        private const string PlayerJumpActionPath = "Player/Jump";
        private const string PlayerSprintActionPath = "Player/Sprint";
        private const string PlayerPreviousActionPath = "Player/Previous";
        private const string UiCancelActionPath = "UI/Cancel";
        private const string UiPauseActionPath = "UI/Pause";

        [SerializeField]
        private int maxPlayers = 2;

        [SerializeField]
        private bool allowKeyboardForPlayer0 = true;

        private bool _isUiInputMode;

        [Header("Look Tuning")] [SerializeField]
        private float controllerLookDegreesPerSecond = 500f;

        [SerializeField] [Range(0f, 0.95f)]
        private float controllerLookDeadzone = 0.15f;

        [SerializeField]
        private float controllerLookExponent = 1.35f;

        [SerializeField] [Range(0f, 1f)]
        private float controllerLookActivationThreshold = 0.01f;

        private PlayerInputCollection[] playerActions;
        private static Type _generatedActionsType;
        private static bool _loggedMissingGeneratedActionsType;

        private sealed class PlayerInputCollection
        {
            public IInputActionCollection2 Collection;
            public IDisposable Disposable;
            public InputAction Move;
            public InputAction Look;
            public InputAction Jump;
            public InputAction Sprint;
            public InputAction Previous;
            public InputAction Cancel;
            public InputAction Pause;

            public ReadOnlyArray<InputDevice>? Devices
            {
                get => Collection.devices;
                set => Collection.devices = value;
            }

            public void Enable()
            {
                Collection.Enable();
            }

            public void Disable()
            {
                Collection.Disable();
            }

            public void Dispose()
            {
                Disposable?.Dispose();
            }
        }

        public void InitializeService()
        {
            SetupPlayers();
        }

        public void StartService()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
            ApplyDevices();
        }

        public void CleanupService()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
            DisablePlayers();
            DisposePlayers();
        }

        public Vector2 GetMovement(int player)
        {
            if (_isUiInputMode)
                return Vector2.zero;

            if (!TryGetPlayerActions(player, out var actions))
                return Vector2.zero;

            return actions.Move != null ? actions.Move.ReadValue<Vector2>() : Vector2.zero;
        }

        public Vector2 GetLook(int player)
        {
            if (!TryGetPlayerActions(player, out var actions))
                return Vector2.zero;

            Vector2 rawLook;
            if (TryGetAssignedGamepad(player, out var gamepad) && gamepad != null)
            {
                var stick = gamepad.rightStick.ReadValue();
                var threshold = Mathf.Clamp01(controllerLookActivationThreshold);
                rawLook = stick.sqrMagnitude >= threshold * threshold
                    ? ScaleControllerLook(stick)
                    : actions.Look != null ? actions.Look.ReadValue<Vector2>() : Vector2.zero;
            }
            else
            {
                rawLook = actions.Look != null ? actions.Look.ReadValue<Vector2>() : Vector2.zero;
            }

            return new Vector2(-rawLook.y, rawLook.x);
        }

        public bool GetJumpPressed(int player)
        {
            if (!TryGetPlayerActions(player, out var actions))
                return false;

            return actions.Jump != null && actions.Jump.WasPressedThisFrame();
        }

        public bool GetSprinting(int player)
        {
            if (!TryGetPlayerActions(player, out var actions))
                return false;

            return actions.Sprint != null && actions.Sprint.IsPressed();
        }

        public bool GetDashing(int player)
        {
            return GetSprinting(player);
        }

        public bool GetMenuCancelPressed(int player = 0)
        {
            if (!TryGetPlayerActions(player, out var actions))
                return false;

            // Primary UI back/cancel action (ESC / gamepad B / platform cancel).
            if (actions.Cancel != null && actions.Cancel.WasPressedThisFrame())
                return true;

            // Fallback for projects that route back through Player map bindings.
            return actions.Previous != null && actions.Previous.WasPressedThisFrame();
        }

        public bool GetMenuPausePressed(int player = 0)
        {
            if (!TryGetPlayerActions(player, out var actions))
                return false;

            return actions.Pause != null && actions.Pause.WasPressedThisFrame();
        }

        public void SwitchToUIMode()
        {
            _isUiInputMode = true;
        }

        public void SwitchToGameMode()
        {
            _isUiInputMode = false;
        }

        private void SetupPlayers()
        {
            var playerCount = Mathf.Max(1, maxPlayers);
            playerActions = new PlayerInputCollection[playerCount];
            for (var i = 0; i < playerActions.Length; i++)
                if (!TryCreatePlayerActions(out playerActions[i]))
                {
                    playerActions[i] = null;
                    Debug.LogError(
                        $"[GameInputManager] Failed to create input action collection for player {i}. Check InputSystem_Actions generation.");
                }

            ApplyDevices();
        }

        private void DisablePlayers()
        {
            if (playerActions == null)
                return;

            for (var i = 0; i < playerActions.Length; i++)
            {
                var actions = playerActions[i];
                if (actions == null)
                    continue;

                actions.Disable();
            }
        }

        private void DisposePlayers()
        {
            if (playerActions == null)
                return;

            for (var i = 0; i < playerActions.Length; i++)
                playerActions[i]?.Dispose();
            playerActions = null;
        }

        private void ApplyDevices()
        {
            if (playerActions == null)
                return;

            for (var i = 0; i < playerActions.Length; i++)
            {
                var actions = playerActions[i];
                if (actions == null)
                    continue;

                var devices = new List<InputDevice>(2);
                var gamepad = i < Gamepad.all.Count ? Gamepad.all[i] : null;
                if (gamepad != null)
                    devices.Add(gamepad);

                if (i == 0 && allowKeyboardForPlayer0)
                {
                    if (Keyboard.current != null)
                        devices.Add(Keyboard.current);
                    if (Mouse.current != null)
                        devices.Add(Mouse.current);
                }

                if (devices.Count > 0)
                {
                    actions.Devices = devices.ToArray();
                    actions.Enable();
                }
                else
                {
                    actions.Devices = null;
                    actions.Disable();
                }
            }
        }

        private bool TryGetPlayerActions(int player, out PlayerInputCollection actions)
        {
            actions = null;
            if (playerActions == null)
                return false;
            if (player < 0 || player >= playerActions.Length)
                return false;

            actions = playerActions[player];
            return actions != null;
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Reconnected:
                    ApplyDevices();
                    break;
            }
        }

        private bool TryGetAssignedGamepad(int player, out Gamepad gamepad)
        {
            gamepad = null;
            if (!TryGetPlayerActions(player, out var actions))
                return false;

            var maybeDevices = actions.Devices;
            if (!maybeDevices.HasValue)
                return false;

            var devices = maybeDevices.Value;
            for (var i = 0; i < devices.Count; i++)
                if (devices[i] is Gamepad assignedGamepad)
                {
                    gamepad = assignedGamepad;
                    return true;
                }

            return false;
        }

        private static bool TryCreatePlayerActions(out PlayerInputCollection playerInputCollection)
        {
            playerInputCollection = null;

            if (!TryResolveGeneratedActionsType(out var generatedActionsType))
                return false;

            object instance;
            try
            {
                instance = Activator.CreateInstance(generatedActionsType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameInputManager] Failed to instantiate {generatedActionsType.FullName}: {ex}");
                return false;
            }

            if (instance is not IInputActionCollection2 collection)
            {
                Debug.LogError(
                    $"[GameInputManager] Generated action wrapper {generatedActionsType.FullName} does not implement IInputActionCollection2.");
                (instance as IDisposable)?.Dispose();
                return false;
            }

            var move = collection.FindAction(PlayerMoveActionPath, false);
            var look = collection.FindAction(PlayerLookActionPath, false);
            var jump = collection.FindAction(PlayerJumpActionPath, false);
            var sprint = collection.FindAction(PlayerSprintActionPath, false);
            var previous = collection.FindAction(PlayerPreviousActionPath, false);
            var cancel = collection.FindAction(UiCancelActionPath, false);
            var pause = collection.FindAction(UiPauseActionPath, false);

            if (move == null || look == null || jump == null || sprint == null || previous == null || cancel == null || pause == null)
            {
                Debug.LogError(
                    "[GameInputManager] Input action asset is missing one or more required actions (Player/Move, Player/Look, Player/Jump, Player/Sprint, Player/Previous, UI/Cancel, UI/Pause).");
                (instance as IDisposable)?.Dispose();
                return false;
            }

            playerInputCollection = new PlayerInputCollection
            {
                Collection = collection,
                Disposable = instance as IDisposable,
                Move = move,
                Look = look,
                Jump = jump,
                Sprint = sprint,
                Previous = previous,
                Cancel = cancel,
                Pause = pause
            };

            return true;
        }

        private static bool TryResolveGeneratedActionsType(out Type generatedActionsType)
        {
            generatedActionsType = _generatedActionsType;
            if (generatedActionsType != null)
                return true;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(GeneratedActionsTypeName, false);
                if (type == null)
                    continue;

                _generatedActionsType = type;
                _loggedMissingGeneratedActionsType = false;
                generatedActionsType = type;
                return true;
            }

            if (!_loggedMissingGeneratedActionsType)
            {
                _loggedMissingGeneratedActionsType = true;
                Debug.LogError(
                    $"[GameInputManager] Could not resolve generated '{GeneratedActionsTypeName}' type. Ensure Assets/InputSystem_Actions.cs is generated and free of compile errors.");
            }

            return false;
        }

        private Vector2 ScaleControllerLook(Vector2 stick)
        {
            var magnitude = stick.magnitude;
            var deadzone = Mathf.Clamp01(controllerLookDeadzone);
            if (magnitude <= deadzone)
                return Vector2.zero;

            var normalized = Mathf.InverseLerp(deadzone, 1f, magnitude);
            var curved = Mathf.Pow(normalized, Mathf.Max(0.01f, controllerLookExponent));
            var scaledMagnitude = curved * Mathf.Max(0f, controllerLookDegreesPerSecond) * Time.unscaledDeltaTime;
            var direction = magnitude > 0f ? stick / magnitude : Vector2.zero;
            return direction * scaledMagnitude;
        }
    }
}
