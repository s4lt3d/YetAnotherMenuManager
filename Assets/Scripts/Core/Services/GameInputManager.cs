using Core.Interfaces;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Managers
{
    public class GameInputManager : MonoBehaviour, IService
    {
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

        private InputSystem_Actions[] playerActions;

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

            return actions.Player.Move.ReadValue<Vector2>();
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
                    : actions.Player.Look.ReadValue<Vector2>();
            }
            else
            {
                rawLook = actions.Player.Look.ReadValue<Vector2>();
            }

            return new Vector2(-rawLook.y, rawLook.x);
        }

        public bool GetJumpPressed(int player)
        {
            if (!TryGetPlayerActions(player, out var actions))
                return false;

            return actions.Player.Jump.WasPressedThisFrame();
        }

        public bool GetSprinting(int player)
        {
            if (!TryGetPlayerActions(player, out var actions))
                return false;

            return actions.Player.Sprint.IsPressed();
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
            if (actions.UI.Cancel.WasPressedThisFrame())
                return true;

            // Fallback for projects that route back through Player map bindings.
            return actions.Player.Previous.WasPressedThisFrame();
        }

        public bool GetMenuPausePressed(int player = 0)
        {
            if (!TryGetPlayerActions(player, out var actions))
                return false;

            return actions.UI.Pause.WasPressedThisFrame();
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
            playerActions = new InputSystem_Actions[playerCount];
            for (var i = 0; i < playerActions.Length; i++)
                playerActions[i] = new InputSystem_Actions();

            ApplyDevices();
        }

        private void DisablePlayers()
        {
            if (playerActions == null)
                return;

            for (var i = 0; i < playerActions.Length; i++)
            {
                playerActions[i].Player.Disable();
                playerActions[i].UI.Disable();
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
                    playerActions[i].devices = devices.ToArray();
                    playerActions[i].Player.Enable();
                    playerActions[i].UI.Enable();
                }
                else
                {
                    playerActions[i].devices = null;
                    playerActions[i].Player.Disable();
                    playerActions[i].UI.Disable();
                }
            }
        }

        private bool TryGetPlayerActions(int player, out InputSystem_Actions actions)
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

            var maybeDevices = actions.devices;
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
