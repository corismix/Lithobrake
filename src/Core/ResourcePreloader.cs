using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lithobrake.Core
{
    /// <summary>
    /// Resource preloading system for optimal game performance
    /// Preloads commonly used resources and warms up shaders during loading
    /// </summary>
    public static class ResourcePreloader
    {
        private static readonly Dictionary<string, Resource> _preloadedResources = new();
        private static readonly Dictionary<string, Shader> _preloadedShaders = new();
        private static bool _isInitialized = false;
        
        // Common resource paths that should be preloaded
        private static readonly string[] CommonResourcePaths = {
            "res://resources/parts/meshes/engine.obj",
            "res://resources/parts/meshes/fuel-tank.obj", 
            "res://resources/parts/meshes/command-pod.obj",
            "res://resources/materials/metal.tres",
            "res://resources/materials/rocket_exhaust.tres",
            "res://resources/audio/engine_fire.ogg",
            "res://resources/audio/staging.ogg"
        };
        
        // Shader paths that need warmup
        private static readonly string[] ShaderPaths = {
            "res://shaders/exhaust_particles.gdshader",
            "res://shaders/heating_effect.gdshader",
            "res://shaders/atmospheric_glow.gdshader",
            "res://shaders/vapor_trail.gdshader"
        };
        
        /// <summary>
        /// Initialize resource preloading system
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (_isInitialized)
                return;
                
            GD.Print("ResourcePreloader: Starting resource preloading...");
            var startTime = Time.GetTicksMsec();
            
            // Preload common resources
            await PreloadCommonResourcesAsync();
            
            // Warm up shaders
            await WarmupShadersAsync();
            
            var duration = Time.GetTicksMsec() - startTime;
            GD.Print($"ResourcePreloader: Initialization complete in {duration}ms");
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// Preload commonly used resources
        /// </summary>
        private static async Task PreloadCommonResourcesAsync()
        {
            var preloadTasks = new List<Task>();
            
            foreach (string path in CommonResourcePaths)
            {
                preloadTasks.Add(Task.Run(() => PreloadResource(path)));
            }
            
            await Task.WhenAll(preloadTasks);
            
            GD.Print($"ResourcePreloader: Preloaded {_preloadedResources.Count} common resources");
        }
        
        /// <summary>
        /// Preload a single resource
        /// </summary>
        private static void PreloadResource(string path)
        {
            try
            {
                if (ResourceLoader.Exists(path))
                {
                    var resource = GD.Load(path);
                    if (resource != null)
                    {
                        lock (_preloadedResources)
                        {
                            _preloadedResources[path] = resource;
                        }
                        GD.Print($"ResourcePreloader: Preloaded {path}");
                    }
                }
                else
                {
                    GD.PrintErr($"ResourcePreloader: Resource not found: {path}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"ResourcePreloader: Failed to preload {path}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Warm up particle effect shaders
        /// </summary>
        private static async Task WarmupShadersAsync()
        {
            var warmupTasks = new List<Task>();
            
            foreach (string shaderPath in ShaderPaths)
            {
                warmupTasks.Add(Task.Run(() => WarmupShader(shaderPath)));
            }
            
            await Task.WhenAll(warmupTasks);
            
            GD.Print($"ResourcePreloader: Warmed up {_preloadedShaders.Count} shaders");
        }
        
        /// <summary>
        /// Warm up a single shader by loading and creating a test material
        /// </summary>
        private static void WarmupShader(string shaderPath)
        {
            try
            {
                if (ResourceLoader.Exists(shaderPath))
                {
                    var shader = GD.Load<Shader>(shaderPath);
                    if (shader != null)
                    {
                        // Create a test material to force shader compilation
                        var material = new ShaderMaterial();
                        material.Shader = shader;
                        
                        lock (_preloadedShaders)
                        {
                            _preloadedShaders[shaderPath] = shader;
                        }
                        
                        GD.Print($"ResourcePreloader: Warmed up shader {shaderPath}");
                        
                        // Clean up test material
                        material?.Dispose();
                    }
                }
                else
                {
                    GD.Print($"ResourcePreloader: Optional shader not found: {shaderPath}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"ResourcePreloader: Failed to warm up shader {shaderPath}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get a preloaded resource, falling back to regular loading if not preloaded
        /// </summary>
        public static T? GetResource<T>(string path) where T : Resource
        {
            // Check preloaded resources first
            lock (_preloadedResources)
            {
                if (_preloadedResources.TryGetValue(path, out var preloadedResource))
                {
                    return preloadedResource as T;
                }
            }
            
            // Fall back to regular loading
            try
            {
                return GD.Load<T>(path);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"ResourcePreloader: Failed to load resource {path}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get a preloaded shader, falling back to regular loading if not preloaded
        /// </summary>
        public static Shader? GetShader(string path)
        {
            // Check preloaded shaders first
            lock (_preloadedShaders)
            {
                if (_preloadedShaders.TryGetValue(path, out var preloadedShader))
                {
                    return preloadedShader;
                }
            }
            
            // Fall back to regular loading
            try
            {
                return GD.Load<Shader>(path);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"ResourcePreloader: Failed to load shader {path}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Preload additional resources at runtime
        /// </summary>
        public static void PreloadAdditionalResource(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            Task.Run(() => PreloadResource(path));
        }
        
        /// <summary>
        /// Clear preloaded resources to free memory
        /// </summary>
        public static void ClearPreloadedResources()
        {
            lock (_preloadedResources)
            {
                foreach (var resource in _preloadedResources.Values)
                {
                    resource?.Dispose();
                }
                _preloadedResources.Clear();
            }
            
            lock (_preloadedShaders)
            {
                foreach (var shader in _preloadedShaders.Values)
                {
                    shader?.Dispose();
                }
                _preloadedShaders.Clear();
            }
            
            _isInitialized = false;
            GD.Print("ResourcePreloader: Cleared all preloaded resources");
        }
        
        /// <summary>
        /// Get preloading statistics
        /// </summary>
        public static (int resources, int shaders, bool initialized) GetStats()
        {
            lock (_preloadedResources)
            {
                lock (_preloadedShaders)
                {
                    return (_preloadedResources.Count, _preloadedShaders.Count, _isInitialized);
                }
            }
        }
        
        /// <summary>
        /// Validate that a resource path is safe (enhanced security validation)
        /// </summary>
        public static bool ValidateResourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
                
            // Must start with res:// for security
            if (!path.StartsWith("res://"))
                return false;
                
            // No path traversal attempts
            if (path.Contains("..") || path.Contains("\\"))
                return false;
                
            // No absolute paths or drive letters
            if (path.Contains(":") && !path.StartsWith("res://"))
                return false;
                
            // Only allow specific file extensions
            var allowedExtensions = new[] { ".obj", ".tres", ".ogg", ".wav", ".png", ".jpg", ".gdshader", ".tscn", ".gd" };
            bool hasValidExtension = false;
            
            foreach (var ext in allowedExtensions)
            {
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    hasValidExtension = true;
                    break;
                }
            }
            
            if (!hasValidExtension)
            {
                GD.PrintErr($"ResourcePreloader: Invalid file extension in path: {path}");
                return false;
            }
            
            return true;
        }
    }
}