using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FF
{
    public class DebugConsole : MonoBehaviour
    {
        private const int MaxLogEntries = 50;
        private const string InputControlName = "DebugConsoleInput";

        private readonly struct CommandInfo
        {
            public string Usage { get; }
            public string Description { get; }

            public CommandInfo(string usage, string description)
            {
                Usage = usage;
                Description = description;
            }
        }

        private static bool? _debugEnabled;

        private readonly Dictionary<string, CommandInfo> _commandInfo = new(StringComparer.OrdinalIgnoreCase)
        {
            ["help"] = new CommandInfo("help", "Show available commands."),
            ["wave"] = new CommandInfo("wave [number]", "Start the next wave or a specific wave."),
            ["god"] = new CommandInfo("god", "Toggle god mode."),
            ["health"] = new CommandInfo("health", "Restore player health to full."),
            ["tp"] = new CommandInfo("tp [x y]", "Teleport to coordinates, or to the mouse position if no coordinates are provided.")
        };

        private readonly List<string> _logs = new();
        private string _input = string.Empty;
        private bool _visible;
        private bool _focusInput;
        private Health _playerHealth;
        private bool _godMode;
        private Vector2 _logScroll;

        public static bool IsDebugEnabled
        {
            get
            {
                if (_debugEnabled.HasValue)
                {
                    return _debugEnabled.Value;
                }

                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "-debug", StringComparison.OrdinalIgnoreCase))
                    {
                        _debugEnabled = true;
                        return true;
                    }
                }

                _debugEnabled = false;
                return false;
            }
        }

        private void Awake()
        {
            if (!IsDebugEnabled)
            {
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            AppendLog("Debug console enabled. Use `help` for a list of commands.");
            CachePlayerHealth();
        }

        private void OnEnable()
        {
            PlayerController.OnPlayerReady += HandlePlayerReady;
        }

        private void OnDisable()
        {
            PlayerController.OnPlayerReady -= HandlePlayerReady;
            if (_playerHealth != null)
            {
                _playerHealth.OnDamaged -= HandlePlayerDamaged;
            }
        }

        private void Update()
        {
            if (!IsDebugEnabled)
            {
                return;
            }

            if (WasTogglePressed())
            {
                _visible = !_visible;
                _focusInput = _visible;
            }
        }

        private void OnGUI()
        {
            if (!IsDebugEnabled || !_visible)
            {
                return;
            }

            float width = Mathf.Min(600f, Screen.width - 20f);
            Rect boxRect = new Rect(10f, 10f, width, 200f);
            GUI.Box(boxRect, "Debug Console");

            Rect logRect = new Rect(20f, 40f, width - 20f, 120f);
            GUILayout.BeginArea(logRect);
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Width(logRect.width), GUILayout.Height(logRect.height));
            for (int i = 0; i < _logs.Count; i++)
            {
                GUILayout.Label(_logs[i]);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            Rect inputRect = new Rect(20f, 165f, width - 40f, 25f);
            GUI.SetNextControlName(InputControlName);
            _input = GUI.TextField(inputRect, _input);

            if (_focusInput)
            {
                GUI.FocusControl(InputControlName);
                _focusInput = false;
            }

            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
            {
                ExecuteCommand(_input);
                _input = string.Empty;
                evt.Use();
            }
        }

        private bool WasTogglePressed()
        {
            if (Keyboard.current != null)
            {
                return Keyboard.current.backquoteKey.wasPressedThisFrame;
            }

            return Input.GetKeyDown(KeyCode.BackQuote);
        }

        private void ExecuteCommand(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return;
            }

            AppendLog($"> {commandLine}");
            string[] parts = commandLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();

            switch (command)
            {
                case "help":
                    HandleHelp();
                    break;
                case "wave":
                    if (parts.Length == 1)
                    {
                        HandleWave();
                        break;
                    }
                    if (parts.Length == 2 && int.TryParse(parts[1], out int targetWave) && targetWave > 0)
                    {
                        HandleWave(targetWave);
                        break;
                    }
                    if (parts.Length > 1)
                    {
                        AppendUsage("wave");
                        break;
                    }
                    break;
                case "god":
                    if (parts.Length > 1)
                    {
                        AppendUsage("god");
                        break;
                    }
                    ToggleGodMode();
                    break;
                case "health":
                    if (parts.Length > 1)
                    {
                        AppendUsage("health");
                        break;
                    }
                    HandleHealth();
                    break;
                case "tp":
                    HandleTeleport(parts);
                    break;
                default:
                    AppendLog("Unknown command. Use `help` to see available commands.");
                    break;
            }
        }

        private void HandleHelp()
        {
            AppendLog("Available commands:");
            foreach (CommandInfo info in _commandInfo.Values)
            {
                AppendLog($"{info.Usage} - {info.Description}");
            }
        }

        private void AppendUsage(string command)
        {
            if (_commandInfo.TryGetValue(command, out CommandInfo info))
            {
                AppendLog($"Usage: {info.Usage}");
            }
        }

        private void HandleWave()
        {
            if (!GameManager.I)
            {
                AppendLog("GameManager not found.");
                return;
            }

            bool started = GameManager.I.DebugStartNextWave();
            AppendLog(started ? $"Started wave {GameManager.I.Wave}." : "Unable to start wave.");
        }

        private void HandleWave(int wave)
        {
            if (!GameManager.I)
            {
                AppendLog("GameManager not found.");
                return;
            }

            bool started = GameManager.I.DebugStartWave(wave);
            AppendLog(started ? $"Started wave {GameManager.I.Wave}." : "Unable to start wave.");
        }

        private void ToggleGodMode()
        {
            SetGodMode(!_godMode);
            AppendLog(_godMode ? "God mode enabled." : "God mode disabled.");
        }

        private void SetGodMode(bool enabled)
        {
            if (_godMode == enabled)
            {
                return;
            }

            if (_playerHealth != null)
            {
                _playerHealth.OnDamaged -= HandlePlayerDamaged;
            }

            _godMode = enabled;

            if (_playerHealth != null && _godMode)
            {
                _playerHealth.OnDamaged += HandlePlayerDamaged;
            }
        }

        private void HandlePlayerDamaged(int amount)
        {
            if (!_godMode || _playerHealth == null)
            {
                return;
            }

            _playerHealth.Heal(amount);
        }

        private void HandleHealth()
        {
            if (!EnsurePlayerHealth())
            {
                AppendLog("Player health not found.");
                return;
            }

            bool wasMissing = _playerHealth.CurrentHP < _playerHealth.MaxHP;
            _playerHealth.Heal(_playerHealth.MaxHP);
            AppendLog(wasMissing ? "Healed to full." : "Already at full health.");
        }

        private void HandleTeleport(string[] parts)
        {
            if (!EnsurePlayerHealth())
            {
                AppendLog("Player not found.");
                return;
            }

            Transform playerTransform = _playerHealth.transform;
            Vector3 destination;

            if (parts.Length == 1)
            {
                Camera camera = Camera.main;
                if (!camera)
                {
                    AppendLog("Camera not found.");
                    return;
                }

                Vector2 screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
                Vector3 worldPos = camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, camera.nearClipPlane));
                destination = new Vector3(worldPos.x, worldPos.y, playerTransform.position.z);
            }
            else if (parts.Length == 3
                && float.TryParse(parts[1], out float x)
                && float.TryParse(parts[2], out float y))
            {
                destination = new Vector3(x, y, playerTransform.position.z);
            }
            else
            {
                AppendUsage("tp");
                return;
            }

            if (playerTransform.TryGetComponent(out Rigidbody2D rigidbody))
            {
                rigidbody.position = destination;
            }
            else
            {
                playerTransform.position = destination;
            }

            AppendLog($"Teleported to {destination.x:0.##}, {destination.y:0.##}.");
        }

        private bool EnsurePlayerHealth()
        {
            if (_playerHealth != null)
            {
                return true;
            }

            CachePlayerHealth();
            return _playerHealth != null;
        }

        private void CachePlayerHealth()
        {
            PlayerController controller = FindFirstObjectByType<PlayerController>();
            _playerHealth = controller ? controller.GetComponentInChildren<Health>() : FindFirstObjectByType<Health>();
        }

        private void HandlePlayerReady(PlayerController controller)
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDamaged -= HandlePlayerDamaged;
            }

            _playerHealth = controller ? controller.GetComponentInChildren<Health>() : null;

            if (_playerHealth != null && _godMode)
            {
                _playerHealth.OnDamaged += HandlePlayerDamaged;
            }
        }

        private void AppendLog(string message)
        {
            _logs.Add(message);
            if (_logs.Count > MaxLogEntries)
            {
                _logs.RemoveAt(0);
            }

            _logScroll.y = float.MaxValue;
        }
    }
}
