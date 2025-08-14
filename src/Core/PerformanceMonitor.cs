using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lithobrake.Core
{
    /// <summary>
    /// Real-time performance monitoring system for tracking frame times, physics performance,
    /// and system metrics according to project performance targets.
    /// </summary>
    public partial class PerformanceMonitor : CanvasLayer
    {
        private Label _performanceLabel;
        private Stopwatch _frameTimer = new Stopwatch();
        private Stopwatch _physicsTimer = new Stopwatch();
        private Stopwatch _scriptTimer = new Stopwatch();
        
        // Performance tracking
        private Queue<double> _frameTimeSamples = new Queue<double>();
        private Queue<double> _physicsTimeSamples = new Queue<double>();
        private Queue<double> _scriptTimeSamples = new Queue<double>();
        private const int SampleCount = 60; // 1 second of samples at 60fps
        
        // Performance targets from CLAUDE.md
        private const double TARGET_FRAME_TIME = 16.6; // ms (60 FPS)
        private const double TARGET_PHYSICS_TIME = 5.0; // ms
        private const double TARGET_SCRIPT_TIME = 3.0; // ms
        
        // API validation metrics
        private double _lastConversionBenchmark = 0;
        private bool _apiValidationComplete = false;
        private string _apiStatus = "Not Tested";
        
        public override void _Ready()
        {
            // Create UI overlay
            _performanceLabel = new Label();
            _performanceLabel.Position = new Vector2(10, 10);
            _performanceLabel.AddThemeStyleboxOverride("normal", new StyleBoxFlat());
            var styleBox = _performanceLabel.GetThemeStylebox("normal") as StyleBoxFlat;
            if (styleBox != null)
            {
                styleBox.BgColor = new Color(0, 0, 0, 0.7f);
                styleBox.ContentMarginLeft = 8;
                styleBox.ContentMarginRight = 8;
                styleBox.ContentMarginTop = 4;
                styleBox.ContentMarginBottom = 4;
            }
            
            AddChild(_performanceLabel);
            
            // Start frame timing
            _frameTimer.Start();
            
            GD.Print("PerformanceMonitor initialized");
        }
        
        public override void _Process(double delta)
        {
            // Measure script time
            _scriptTimer.Restart();
            
            // Record frame time
            RecordSample(_frameTimeSamples, _frameTimer.Elapsed.TotalMilliseconds);
            _frameTimer.Restart();
            
            // Update display
            UpdatePerformanceDisplay();
            
            _scriptTimer.Stop();
            RecordSample(_scriptTimeSamples, _scriptTimer.Elapsed.TotalMilliseconds);
        }
        
        public override void _PhysicsProcess(double delta)
        {
            _physicsTimer.Restart();
            
            // Minimal physics processing for measurement
            // Actual physics work would go here
            
            _physicsTimer.Stop();
            RecordSample(_physicsTimeSamples, _physicsTimer.Elapsed.TotalMilliseconds);
        }
        
        private void RecordSample(Queue<double> samples, double value)
        {
            samples.Enqueue(value);
            if (samples.Count > SampleCount)
                samples.Dequeue();
        }
        
        private double GetAverageTime(Queue<double> samples)
        {
            if (samples.Count == 0) return 0;
            
            double sum = 0;
            foreach (double sample in samples)
                sum += sample;
            
            return sum / samples.Count;
        }
        
        private string GetPerformanceStatus(double actual, double target)
        {
            if (actual <= target)
                return "✅";
            else if (actual <= target * 1.2)
                return "⚠️";
            else
                return "❌";
        }
        
        private void UpdatePerformanceDisplay()
        {
            double avgFrameTime = GetAverageTime(_frameTimeSamples);
            double avgPhysicsTime = GetAverageTime(_physicsTimeSamples);
            double avgScriptTime = GetAverageTime(_scriptTimeSamples);
            
            double currentFPS = avgFrameTime > 0 ? 1000.0 / avgFrameTime : 0;
            
            string performanceText = $"PERFORMANCE MONITOR\n";
            performanceText += $"FPS: {currentFPS:F1} {GetPerformanceStatus(avgFrameTime, TARGET_FRAME_TIME)}\n";
            performanceText += $"Frame: {avgFrameTime:F2}ms (target: {TARGET_FRAME_TIME}ms)\n";
            performanceText += $"Physics: {avgPhysicsTime:F2}ms (target: {TARGET_PHYSICS_TIME}ms) {GetPerformanceStatus(avgPhysicsTime, TARGET_PHYSICS_TIME)}\n";
            performanceText += $"Script: {avgScriptTime:F2}ms (target: {TARGET_SCRIPT_TIME}ms) {GetPerformanceStatus(avgScriptTime, TARGET_SCRIPT_TIME)}\n\n";
            
            performanceText += $"API VALIDATION\n";
            performanceText += $"Status: {_apiStatus}\n";
            if (_lastConversionBenchmark > 0)
            {
                performanceText += $"Conversion: {_lastConversionBenchmark:F3}ms/1000ops\n";
                performanceText += $"Target: <0.1ms/1000ops {GetPerformanceStatus(_lastConversionBenchmark, 0.1)}\n";
            }
            
            _performanceLabel.Text = performanceText;
        }
        
        /// <summary>
        /// Benchmark Double3 ↔ Vector3 conversion performance
        /// </summary>
        public double BenchmarkConversions(int iterations = 1000)
        {
            var stopwatch = Stopwatch.StartNew();
            var double3Array = new Double3[iterations];
            var vector3Array = new Vector3[iterations];
            
            // Initialize test data
            var random = new Random(42); // Deterministic seed
            for (int i = 0; i < iterations; i++)
            {
                double3Array[i] = new Double3(random.NextDouble() * 1000, random.NextDouble() * 1000, random.NextDouble() * 1000);
            }
            
            stopwatch.Restart();
            
            // Benchmark Double3 → Vector3 conversions
            for (int i = 0; i < iterations; i++)
            {
                vector3Array[i] = double3Array[i].ToVector3();
            }
            
            // Benchmark Vector3 → Double3 conversions
            for (int i = 0; i < iterations; i++)
            {
                double3Array[i] = Double3.FromVector3(vector3Array[i]);
            }
            
            stopwatch.Stop();
            _lastConversionBenchmark = stopwatch.Elapsed.TotalMilliseconds;
            
            GD.Print($"Conversion benchmark: {_lastConversionBenchmark:F3}ms for {iterations * 2} operations");
            GD.Print($"Performance: {(_lastConversionBenchmark / iterations * 500):F6}ms per 1000 operations");
            
            return _lastConversionBenchmark;
        }
        
        /// <summary>
        /// Update API validation status
        /// </summary>
        public void SetApiValidationStatus(string status)
        {
            _apiStatus = status;
            _apiValidationComplete = status.Contains("Complete");
        }
        
        /// <summary>
        /// Get current performance metrics
        /// </summary>
        public PerformanceMetrics GetCurrentMetrics()
        {
            return new PerformanceMetrics
            {
                FrameTime = GetAverageTime(_frameTimeSamples),
                PhysicsTime = GetAverageTime(_physicsTimeSamples),
                ScriptTime = GetAverageTime(_scriptTimeSamples),
                FPS = GetAverageTime(_frameTimeSamples) > 0 ? 1000.0 / GetAverageTime(_frameTimeSamples) : 0,
                ConversionBenchmark = _lastConversionBenchmark,
                ApiValidationComplete = _apiValidationComplete
            };
        }
    }
    
    /// <summary>
    /// Structure for holding performance metrics
    /// </summary>
    public struct PerformanceMetrics
    {
        public double FrameTime;
        public double PhysicsTime;
        public double ScriptTime;
        public double FPS;
        public double ConversionBenchmark;
        public bool ApiValidationComplete;
    }
}