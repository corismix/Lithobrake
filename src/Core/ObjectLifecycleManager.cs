using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using Godot;

namespace Lithobrake.Core
{
    /// <summary>
    /// Centralized object lifecycle management system for proper tracking and cleanup of Godot objects.
    /// Eliminates the need for excessive IsInstanceValid() checks by implementing proper state tracking.
    /// </summary>
    public static class ObjectLifecycleManager
    {
        // Thread-safe collections for lifecycle tracking
        private static readonly ConcurrentDictionary<uint, ObjectState> _objectStates = new();
        private static readonly ConcurrentDictionary<uint, List<Action<uint>>> _disposeCallbacks = new();
        private static readonly object _callbackLock = new();
        
        // Performance tracking
        private static long _trackedObjectCount = 0;
        private static long _disposedObjectCount = 0;
        
        /// <summary>
        /// Object lifecycle states
        /// </summary>
        public enum ObjectState
        {
            Created,    // Object created and valid
            Active,     // Object active and in use
            Disposing,  // Object disposal in progress
            Disposed    // Object disposed and should not be used
        }
        
        /// <summary>
        /// Registers a Godot object for lifecycle tracking.
        /// Returns a tracking ID that can be used to check state and register callbacks.
        /// </summary>
        /// <param name="obj">The Godot object to track</param>
        /// <param name="name">Optional name for debugging</param>
        /// <returns>Tracking ID for the object</returns>
        public static uint RegisterObject(GodotObject obj, string name = "")
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
                
            uint trackingId = GetTrackingId(obj);
            
            _objectStates.TryAdd(trackingId, ObjectState.Created);
            Interlocked.Increment(ref _trackedObjectCount);
            
            DebugLog.LogResource($"Registered object {trackingId} ({name}) for lifecycle tracking");
            
            return trackingId;
        }
        
        /// <summary>
        /// Marks an object as active (normal operational state).
        /// </summary>
        /// <param name="trackingId">The tracking ID of the object</param>
        public static void MarkActive(uint trackingId)
        {
            _objectStates.TryUpdate(trackingId, ObjectState.Active, ObjectState.Created);
        }
        
        /// <summary>
        /// Marks an object as disposing and triggers disposal callbacks.
        /// Should be called before QueueFree() or manual disposal.
        /// </summary>
        /// <param name="trackingId">The tracking ID of the object</param>
        public static void MarkDisposing(uint trackingId)
        {
            if (_objectStates.TryUpdate(trackingId, ObjectState.Disposing, ObjectState.Active) ||
                _objectStates.TryUpdate(trackingId, ObjectState.Disposing, ObjectState.Created))
            {
                // Trigger disposal callbacks
                TriggerDisposeCallbacks(trackingId);
            }
        }
        
        /// <summary>
        /// Marks an object as fully disposed. Should be called after QueueFree() or disposal completion.
        /// </summary>
        /// <param name="trackingId">The tracking ID of the object</param>
        public static void MarkDisposed(uint trackingId)
        {
            if (_objectStates.TryUpdate(trackingId, ObjectState.Disposed, ObjectState.Disposing))
            {
                Interlocked.Increment(ref _disposedObjectCount);
                CleanupObject(trackingId);
                DebugLog.LogResource($"Object {trackingId} marked as disposed");
            }
        }
        
        /// <summary>
        /// Gets the current state of a tracked object.
        /// </summary>
        /// <param name="trackingId">The tracking ID of the object</param>
        /// <returns>Current object state</returns>
        public static ObjectState GetObjectState(uint trackingId)
        {
            return _objectStates.TryGetValue(trackingId, out var state) ? state : ObjectState.Disposed;
        }
        
        /// <summary>
        /// Checks if an object is safe to use (not disposing or disposed).
        /// </summary>
        /// <param name="trackingId">The tracking ID of the object</param>
        /// <returns>True if object is safe to use</returns>
        public static bool IsObjectUsable(uint trackingId)
        {
            var state = GetObjectState(trackingId);
            return state == ObjectState.Created || state == ObjectState.Active;
        }
        
        /// <summary>
        /// Registers a callback to be invoked when an object is being disposed.
        /// Useful for cleanup of dependent resources.
        /// </summary>
        /// <param name="trackingId">The tracking ID of the object</param>
        /// <param name="callback">Callback to invoke on disposal</param>
        public static void RegisterDisposeCallback(uint trackingId, Action<uint> callback)
        {
            if (callback == null)
                return;
                
            lock (_callbackLock)
            {
                if (!_disposeCallbacks.ContainsKey(trackingId))
                    _disposeCallbacks[trackingId] = new List<Action<uint>>();
                    
                _disposeCallbacks[trackingId].Add(callback);
            }
        }
        
        /// <summary>
        /// Creates a managed wrapper for a Godot object that automatically handles lifecycle.
        /// </summary>
        /// <typeparam name="T">Type of Godot object</typeparam>
        /// <param name="obj">The Godot object to wrap</param>
        /// <param name="name">Optional name for debugging</param>
        /// <returns>Managed wrapper</returns>
        public static ManagedGodotObject<T> CreateManagedObject<T>(T obj, string name = "") where T : GodotObject
        {
            return new ManagedGodotObject<T>(obj, name);
        }
        
        /// <summary>
        /// Gets tracking statistics for monitoring and debugging.
        /// </summary>
        /// <returns>Lifecycle statistics</returns>
        public static LifecycleStatistics GetStatistics()
        {
            return new LifecycleStatistics
            {
                TrackedObjectCount = _trackedObjectCount,
                DisposedObjectCount = _disposedObjectCount,
                ActiveObjectCount = _objectStates.Count,
                CallbackCount = _disposeCallbacks.Values.Sum(list => list.Count)
            };
        }
        
        /// <summary>
        /// Cleans up disposed objects and their callbacks. 
        /// Should be called periodically to prevent memory accumulation.
        /// </summary>
        public static void PerformCleanup()
        {
            var disposedKeys = new List<uint>();
            
            foreach (var kvp in _objectStates)
            {
                if (kvp.Value == ObjectState.Disposed)
                    disposedKeys.Add(kvp.Key);
            }
            
            foreach (var key in disposedKeys)
            {
                CleanupObject(key);
            }
            
            DebugLog.LogResource($"Cleaned up {disposedKeys.Count} disposed object records");
        }
        
        /// <summary>
        /// Performs complete cleanup of all tracked objects and callbacks.
        /// Should be called when shutting down the application.
        /// </summary>
        public static void PerformCompleteCleanup()
        {
            lock (_callbackLock)
            {
                var totalObjects = _objectStates.Count;
                var totalCallbacks = _disposeCallbacks.Values.Sum(list => list.Count);
                
                _objectStates.Clear();
                _disposeCallbacks.Clear();
                
                // Reset counters
                Interlocked.Exchange(ref _trackedObjectCount, 0);
                Interlocked.Exchange(ref _disposedObjectCount, 0);
                
                DebugLog.LogResource($"ObjectLifecycleManager: Complete cleanup - removed {totalObjects} objects and {totalCallbacks} callbacks");
            }
        }
        
        private static uint GetTrackingId(GodotObject obj)
        {
            // Use object's GetInstanceId() for consistent tracking
            return (uint)obj.GetInstanceId();
        }
        
        private static void TriggerDisposeCallbacks(uint trackingId)
        {
            List<Action<uint>>? callbacks = null;
            
            lock (_callbackLock)
            {
                if (_disposeCallbacks.TryGetValue(trackingId, out callbacks))
                {
                    // Create a copy to avoid holding the lock during callback execution
                    callbacks = new List<Action<uint>>(callbacks);
                }
            }
            
            if (callbacks != null)
            {
                foreach (var callback in callbacks)
                {
                    SafeOperations.TryExecute(() => callback(trackingId), 
                        $"ObjectLifecycleManager.DisposeCallback[{trackingId}]");
                }
            }
        }
        
        private static void CleanupObject(uint trackingId)
        {
            _objectStates.TryRemove(trackingId, out _);
            
            lock (_callbackLock)
            {
                _disposeCallbacks.TryRemove(trackingId, out _);
            }
        }
    }
    
    /// <summary>
    /// Managed wrapper for Godot objects that provides automatic lifecycle tracking.
    /// Eliminates the need for manual IsInstanceValid() checks.
    /// </summary>
    /// <typeparam name="T">Type of Godot object</typeparam>
    public class ManagedGodotObject<T> : IDisposable where T : GodotObject
    {
        private readonly uint _trackingId;
        private readonly string _name;
        private T? _object;
        private bool _disposed = false;
        
        internal ManagedGodotObject(T obj, string name = "")
        {
            _object = obj ?? throw new ArgumentNullException(nameof(obj));
            _name = string.IsNullOrEmpty(name) ? typeof(T).Name : name;
            _trackingId = ObjectLifecycleManager.RegisterObject(obj, _name);
            ObjectLifecycleManager.MarkActive(_trackingId);
        }
        
        /// <summary>
        /// Gets the wrapped Godot object if it's still usable.
        /// </summary>
        public T? Object => IsUsable ? _object : null;
        
        /// <summary>
        /// Checks if the wrapped object is still usable.
        /// </summary>
        public bool IsUsable => !_disposed && ObjectLifecycleManager.IsObjectUsable(_trackingId);
        
        /// <summary>
        /// Gets the current lifecycle state of the wrapped object.
        /// </summary>
        public ObjectLifecycleManager.ObjectState State => ObjectLifecycleManager.GetObjectState(_trackingId);
        
        /// <summary>
        /// Safely executes an action on the wrapped object if it's usable.
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <returns>True if action was executed, false if object not usable</returns>
        public bool TryExecute(Action<T> action)
        {
            if (!IsUsable || _object == null)
                return false;
                
            return SafeOperations.TryExecute(() => action(_object), $"ManagedGodotObject<{typeof(T).Name}>[{_name}]");
        }
        
        /// <summary>
        /// Safely executes a function on the wrapped object if it's usable.
        /// </summary>
        /// <typeparam name="TResult">Return type</typeparam>
        /// <param name="func">Function to execute</param>
        /// <returns>Function result if successful, default value if not usable</returns>
        public TResult? TryExecute<TResult>(Func<T, TResult> func) where TResult : class
        {
            if (!IsUsable || _object == null)
                return null;
                
            return SafeOperations.TryExecute(() => func(_object), $"ManagedGodotObject<{typeof(T).Name}>[{_name}]");
        }
        
        /// <summary>
        /// Registers a callback to be invoked when the object is disposed.
        /// </summary>
        /// <param name="callback">Callback to register</param>
        public void RegisterDisposeCallback(Action<uint> callback)
        {
            ObjectLifecycleManager.RegisterDisposeCallback(_trackingId, callback);
        }
        
        /// <summary>
        /// Disposes the wrapped object and updates lifecycle tracking.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            if (_object != null)
            {
                ObjectLifecycleManager.MarkDisposing(_trackingId);
                
                // Queue the object for deletion in Godot if it's a Node
                SafeOperations.TryExecute(() => 
                {
                    if (_object is Node node)
                        node.QueueFree();
                }, $"ManagedGodotObject<{typeof(T).Name}>[{_name}].QueueFree");
                
                ObjectLifecycleManager.MarkDisposed(_trackingId);
                _object = null;
            }
            
            GC.SuppressFinalize(this);
        }
        
        ~ManagedGodotObject()
        {
            Dispose();
        }
    }
    
    /// <summary>
    /// Statistics about object lifecycle management.
    /// </summary>
    public struct LifecycleStatistics
    {
        public long TrackedObjectCount { get; set; }
        public long DisposedObjectCount { get; set; }
        public int ActiveObjectCount { get; set; }
        public int CallbackCount { get; set; }
    }
}