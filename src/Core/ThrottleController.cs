using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithobrake.Core
{
    /// <summary>
    /// Throttle controller managing engine throttle input and smooth transitions.
    /// Handles Z/X keys for 5% incremental steps, Shift+Z for full throttle, 
    /// Ctrl+X for complete cutoff, and smooth throttle response curves.
    /// </summary>
    public partial class ThrottleController : Node
    {
        // Thread-safe singleton pattern for global access
        private static readonly Lazy<ThrottleController> _lazyInstance = new(() => new ThrottleController());
        public static ThrottleController Instance => _lazyInstance.Value;
        
        // Throttle state
        private double _currentThrottle = 0.0;
        private double _targetThrottle = 0.0;
        private bool _throttleChanged = false;
        
        // Input settings from current-task.md
        private const double ThrottleIncrement = 0.05; // 5% steps for Z/X keys
        private const double ThrottleResponseRate = 2.0; // Throttle change rate per second
        private const double MinThrottle = 0.0;
        private const double MaxThrottle = 1.0;
        
        // Smooth transition settings
        private const double SmoothTransitionThreshold = 0.001; // Minimum change for smoothing
        private const double InstantThrottleThreshold = 0.8; // Above this, allow faster response
        
        // Engine management
        private readonly List<Engine> _managedEngines = new();
        private bool _throttleLocked = false;
        
        // Performance tracking
        private double _lastInputTime = 0.0;
        private int _inputEventsProcessed = 0;
        
        public override void _Ready()
        {
            // Thread-safe singleton validation
            if (_lazyInstance.IsValueCreated && _lazyInstance.Value != this)
            {
                GD.PrintErr("ThrottleController: Multiple instances detected!");
                QueueFree();
                return;
            }
            
            GD.Print("ThrottleController: Initialized as singleton");
        }
        
        /// <summary>
        /// Process input events for throttle control
        /// </summary>
        public override void _Input(InputEvent @event)
        {
            if (_throttleLocked)
                return;
            
            var startTime = Time.GetTicksMsec();
            
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                var processed = ProcessKeyInput(keyEvent);
                if (processed)
                {
                    _inputEventsProcessed++;
                    _lastInputTime = Time.GetTicksMsec() - startTime;
                }
            }
        }
        
        /// <summary>
        /// Process throttle updates each frame
        /// </summary>
        public override void _Process(double delta)
        {
            if (_throttleChanged)
            {
                ProcessThrottleTransition(delta);
                ApplyThrottleToEngines();
                _throttleChanged = false;
            }
        }
        
        /// <summary>
        /// Process keyboard input for throttle control
        /// </summary>
        /// <param name="keyEvent">Keyboard input event</param>
        /// <returns>True if input was processed</returns>
        private bool ProcessKeyInput(InputEventKey keyEvent)
        {
            var key = keyEvent.Keycode;
            var shift = keyEvent.ShiftPressed;
            var ctrl = keyEvent.CtrlPressed;
            
            switch (key)
            {
                case Key.Z:
                    if (shift)
                    {
                        // Shift+Z: Full throttle
                        SetThrottle(MaxThrottle);
                        GD.Print("ThrottleController: Full throttle (100%)");
                        return true;
                    }
                    else
                    {
                        // Z: Increase throttle by 5%
                        IncreaseThrottle();
                        return true;
                    }
                    
                case Key.X:
                    if (ctrl)
                    {
                        // Ctrl+X: Complete cutoff
                        SetThrottle(MinThrottle);
                        GD.Print("ThrottleController: Engine cutoff (0%)");
                        return true;
                    }
                    else
                    {
                        // X: Decrease throttle by 5%
                        DecreaseThrottle();
                        return true;
                    }
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Increase throttle by increment
        /// </summary>
        private void IncreaseThrottle()
        {
            var newThrottle = Math.Min(MaxThrottle, _targetThrottle + ThrottleIncrement);
            SetThrottle(newThrottle);
            GD.Print($"ThrottleController: Throttle increased to {newThrottle:P0}");
        }
        
        /// <summary>
        /// Decrease throttle by increment
        /// </summary>
        private void DecreaseThrottle()
        {
            var newThrottle = Math.Max(MinThrottle, _targetThrottle - ThrottleIncrement);
            SetThrottle(newThrottle);
            GD.Print($"ThrottleController: Throttle decreased to {newThrottle:P0}");
        }
        
        /// <summary>
        /// Set throttle level directly
        /// </summary>
        /// <param name="throttle">Throttle level (0-1)</param>
        public void SetThrottle(double throttle)
        {
            var clampedThrottle = Math.Clamp(throttle, MinThrottle, MaxThrottle);
            
            if (Math.Abs(_targetThrottle - clampedThrottle) > SmoothTransitionThreshold)
            {
                _targetThrottle = clampedThrottle;
                _throttleChanged = true;
            }
        }
        
        /// <summary>
        /// Get current throttle level
        /// </summary>
        /// <returns>Throttle level (0-1)</returns>
        public double GetCurrentThrottle()
        {
            return _currentThrottle;
        }
        
        /// <summary>
        /// Get target throttle level
        /// </summary>
        /// <returns>Target throttle level (0-1)</returns>
        public double GetTargetThrottle()
        {
            return _targetThrottle;
        }
        
        /// <summary>
        /// Process smooth throttle transitions
        /// </summary>
        /// <param name="delta">Time step in seconds</param>
        private void ProcessThrottleTransition(double delta)
        {
            if (Math.Abs(_currentThrottle - _targetThrottle) <= SmoothTransitionThreshold)
            {
                _currentThrottle = _targetThrottle;
                return;
            }
            
            // Calculate throttle response rate based on target
            var responseRate = ThrottleResponseRate;
            
            // Allow faster response for high throttle commands (emergency situations)
            if (_targetThrottle > InstantThrottleThreshold || _targetThrottle == 0.0)
            {
                responseRate *= 1.5; // 50% faster for full throttle or cutoff
            }
            
            var maxChange = responseRate * delta;
            
            if (_currentThrottle < _targetThrottle)
            {
                _currentThrottle = Math.Min(_targetThrottle, _currentThrottle + maxChange);
            }
            else
            {
                _currentThrottle = Math.Max(_targetThrottle, _currentThrottle - maxChange);
            }
        }
        
        /// <summary>
        /// Apply current throttle to all managed engines
        /// </summary>
        private void ApplyThrottleToEngines()
        {
            // Clean up null or deleted engines
            for (int i = _managedEngines.Count - 1; i >= 0; i--)
            {
                if (_managedEngines[i] == null || _managedEngines[i].IsQueuedForDeletion())
                {
                    _managedEngines.RemoveAt(i);
                }
            }
            
            // Apply throttle to remaining engines
            foreach (var engine in _managedEngines)
            {
                engine.SetThrottle(_currentThrottle);
            }
        }
        
        /// <summary>
        /// Register an engine for throttle control
        /// </summary>
        /// <param name="engine">Engine to register</param>
        public void RegisterEngine(Engine engine)
        {
            if (engine != null && !_managedEngines.Contains(engine))
            {
                _managedEngines.Add(engine);
                engine.SetThrottle(_currentThrottle); // Apply current throttle immediately
                GD.Print($"ThrottleController: Registered engine {engine.PartName}");
            }
        }
        
        /// <summary>
        /// Unregister an engine from throttle control
        /// </summary>
        /// <param name="engine">Engine to unregister</param>
        public void UnregisterEngine(Engine engine)
        {
            if (_managedEngines.Contains(engine))
            {
                _managedEngines.Remove(engine);
                GD.Print($"ThrottleController: Unregistered engine {engine.PartName}");
            }
        }
        
        /// <summary>
        /// Register all engines in a vessel for throttle control
        /// </summary>
        /// <param name="engines">Engines to register</param>
        public void RegisterEngines(IEnumerable<Engine> engines)
        {
            foreach (var engine in engines)
            {
                if (engine != null)
                {
                    RegisterEngine(engine);
                }
            }
        }
        
        /// <summary>
        /// Clear all registered engines
        /// </summary>
        public void ClearEngines()
        {
            _managedEngines.Clear();
            GD.Print("ThrottleController: Cleared all registered engines");
        }
        
        /// <summary>
        /// Lock throttle control (disable input)
        /// </summary>
        public void LockThrottle()
        {
            _throttleLocked = true;
            GD.Print("ThrottleController: Throttle control locked");
        }
        
        /// <summary>
        /// Unlock throttle control (enable input)
        /// </summary>
        public void UnlockThrottle()
        {
            _throttleLocked = false;
            GD.Print("ThrottleController: Throttle control unlocked");
        }
        
        /// <summary>
        /// Check if throttle is currently transitioning
        /// </summary>
        /// <returns>True if throttle is changing</returns>
        public bool IsThrottleTransitioning()
        {
            return Math.Abs(_currentThrottle - _targetThrottle) > SmoothTransitionThreshold;
        }
        
        /// <summary>
        /// Get throttle controller statistics
        /// </summary>
        /// <returns>Controller statistics</returns>
        public ThrottleControllerStats GetStats()
        {
            return new ThrottleControllerStats
            {
                CurrentThrottle = _currentThrottle,
                TargetThrottle = _targetThrottle,
                IsTransitioning = IsThrottleTransitioning(),
                ManagedEngines = _managedEngines.Count,
                IsLocked = _throttleLocked,
                LastInputTime = _lastInputTime,
                InputEventsProcessed = _inputEventsProcessed
            };
        }
        
        /// <summary>
        /// Reset throttle controller to default state
        /// </summary>
        public void Reset()
        {
            _currentThrottle = 0.0;
            _targetThrottle = 0.0;
            _throttleChanged = false;
            _throttleLocked = false;
            
            // Apply zero throttle to all engines
            ApplyThrottleToEngines();
            
            GD.Print("ThrottleController: Reset to default state");
        }
        
        /// <summary>
        /// Get input help text for throttle controls
        /// </summary>
        /// <returns>Help text string</returns>
        public string GetInputHelpText()
        {
            return "Throttle Controls:\n" +
                   "Z - Increase throttle (+5%)\n" +
                   "X - Decrease throttle (-5%)\n" +
                   "Shift+Z - Full throttle (100%)\n" +
                   "Ctrl+X - Engine cutoff (0%)";
        }
        
        /// <summary>
        /// Cleanup on exit
        /// </summary>
        public override void _ExitTree()
        {
            _managedEngines.Clear();
            
            // Note: Lazy<T> instances cannot be reset, they are cleaned up by GC
            base._ExitTree();
        }
    }
    
    /// <summary>
    /// Throttle controller performance and state statistics
    /// </summary>
    public struct ThrottleControllerStats
    {
        public double CurrentThrottle;
        public double TargetThrottle;
        public bool IsTransitioning;
        public int ManagedEngines;
        public bool IsLocked;
        public double LastInputTime;
        public int InputEventsProcessed;
    }
}