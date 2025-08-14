using Godot;
using System;
using System.Diagnostics;

namespace Lithobrake.Core
{
    /// <summary>
    /// C# test node for validating Node3D integration, signal connectivity, and performance characteristics.
    /// Tests basic lifecycle methods and signal marshaling to establish C#/GDScript integration foundation.
    /// </summary>
    public partial class CSharpTestNode : Node3D
    {
        [Signal]
        public delegate void TestSignalEventHandler(string message, float value);
        
        [Signal]
        public delegate void PackedDataEventHandler(Godot.Collections.Dictionary packedData);
        
        private Stopwatch _updateTimer = new Stopwatch();
        private Stopwatch _physicsTimer = new Stopwatch();
        private double _lastUpdateTime = 0;
        private double _lastPhysicsTime = 0;
        private int _frameCount = 0;
        private int _physicsCount = 0;
        
        // Test data for signal marshaling
        private float _testValue = 0.0f;
        private string _testMessage = "";
        
        // Performance tracking
        private double _cumulativeUpdateTime = 0;
        private double _cumulativePhysicsTime = 0;
        
        public override void _Ready()
        {
            GD.Print("CSharpTestNode: _Ready() called");
            
            // Test signal connectivity
            TestSignal += OnTestSignalReceived;
            PackedData += OnPackedDataReceived;
            
            // Initialize test data
            _testMessage = "C# Node Active";
            _testValue = 42.0f;
            
            // Start performance monitoring
            _updateTimer.Start();
            _physicsTimer.Start();
            
            GD.Print("CSharpTestNode: Ready complete, signals connected");
        }
        
        public override void _Process(double delta)
        {
            _updateTimer.Restart();
            
            // Simulate basic update logic
            _frameCount++;
            _testValue += (float)(delta * 10.0); // Simple animation value
            
            // Test signal emission every 60 frames (~1 second at 60 FPS)
            if (_frameCount % 60 == 0)
            {
                EmitSignal(SignalName.TestSignal, _testMessage, _testValue);
                
                // Test packed signal for performance comparison
                var packedData = new Godot.Collections.Dictionary
                {
                    ["frame_count"] = _frameCount,
                    ["test_value"] = _testValue,
                    ["avg_update_time"] = _cumulativeUpdateTime / _frameCount,
                    ["avg_physics_time"] = _cumulativePhysicsTime / Math.Max(_physicsCount, 1)
                };
                EmitSignal(SignalName.PackedData, packedData);
            }
            
            _updateTimer.Stop();
            _lastUpdateTime = _updateTimer.Elapsed.TotalMilliseconds;
            _cumulativeUpdateTime += _lastUpdateTime;
        }
        
        public override void _PhysicsProcess(double delta)
        {
            _physicsTimer.Restart();
            
            // Simulate basic physics logic
            _physicsCount++;
            
            // Simple position update for testing
            Position = new Vector3(
                (float)Math.Sin(Time.GetTicksUsec() / 1000000.0) * 2.0f,
                (float)Math.Cos(Time.GetTicksUsec() / 1000000.0) * 2.0f,
                0.0f
            );
            
            _physicsTimer.Stop();
            _lastPhysicsTime = _physicsTimer.Elapsed.TotalMilliseconds;
            _cumulativePhysicsTime += _lastPhysicsTime;
        }
        
        private void OnTestSignalReceived(string message, float value)
        {
            GD.Print($"CSharpTestNode: Received test signal - Message: {message}, Value: {value:F2}");
        }
        
        private void OnPackedDataReceived(Godot.Collections.Dictionary packedData)
        {
            GD.Print($"CSharpTestNode: Received packed data with {packedData.Count} entries");
        }
        
        /// <summary>
        /// Get current performance metrics for this node
        /// </summary>
        public NodePerformanceMetrics GetPerformanceMetrics()
        {
            return new NodePerformanceMetrics
            {
                FrameCount = _frameCount,
                PhysicsCount = _physicsCount,
                AverageUpdateTime = _frameCount > 0 ? _cumulativeUpdateTime / _frameCount : 0,
                AveragePhysicsTime = _physicsCount > 0 ? _cumulativePhysicsTime / _physicsCount : 0,
                LastUpdateTime = _lastUpdateTime,
                LastPhysicsTime = _lastPhysicsTime,
                TestValue = _testValue
            };
        }
        
        /// <summary>
        /// Reset performance counters for fresh testing
        /// </summary>
        public void ResetPerformanceCounters()
        {
            _frameCount = 0;
            _physicsCount = 0;
            _cumulativeUpdateTime = 0;
            _cumulativePhysicsTime = 0;
            _lastUpdateTime = 0;
            _lastPhysicsTime = 0;
            
            GD.Print("CSharpTestNode: Performance counters reset");
        }
        
        /// <summary>
        /// Test Double3 conversion performance
        /// </summary>
        public double TestDouble3Conversions(int iterations = 1000)
        {
            var stopwatch = Stopwatch.StartNew();
            var double3Array = new Double3[iterations];
            var vector3Array = new Vector3[iterations];
            
            // Initialize test data
            var random = new Random(42);
            for (int i = 0; i < iterations; i++)
            {
                double3Array[i] = new Double3(
                    random.NextDouble() * 1000000, // Large values for orbital mechanics
                    random.NextDouble() * 1000000,
                    random.NextDouble() * 1000000
                );
            }
            
            stopwatch.Restart();
            
            // Test conversions
            for (int i = 0; i < iterations; i++)
            {
                vector3Array[i] = double3Array[i].ToVector3();
                double3Array[i] = Double3.FromVector3(vector3Array[i]);
            }
            
            stopwatch.Stop();
            double totalTime = stopwatch.Elapsed.TotalMilliseconds;
            
            GD.Print($"CSharpTestNode: {iterations * 2} Double3 conversions took {totalTime:F3}ms");
            GD.Print($"Performance: {(totalTime / iterations * 500):F6}ms per 1000 operations");
            
            return totalTime;
        }
    }
    
    /// <summary>
    /// Performance metrics structure for the test node
    /// </summary>
    public struct NodePerformanceMetrics
    {
        public int FrameCount;
        public int PhysicsCount;
        public double AverageUpdateTime;
        public double AveragePhysicsTime;
        public double LastUpdateTime;
        public double LastPhysicsTime;
        public float TestValue;
    }
}