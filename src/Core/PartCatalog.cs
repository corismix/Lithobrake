using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lithobrake.Core
{
    /// <summary>
    /// JSON-based part catalog system for loading and managing rocket parts.
    /// Supports hot-reload, part instantiation, and catalog validation.
    /// </summary>
    public static class PartCatalog
    {
        // Catalog data
        private static Dictionary<string, PartDefinition> _partDefinitions = new();
        private static Dictionary<PartType, List<string>> _partsByType = new();
        private static bool _catalogLoaded = false;
        private static string _catalogPath = "res://resources/parts/catalog.json";
        
        // Performance monitoring
        private static PerformanceMonitor? _performanceMonitor;
        
        // Hot-reload support
        private static FileSystemWatcher? _fileWatcher;
        private static double _lastReloadTime = 0;
        private static readonly double ReloadCooldown = 0.5; // 500ms cooldown between reloads
        
        // Events
        public static event Action<string>? OnCatalogLoaded;
        public static event Action<string>? OnCatalogReloaded;
        public static event Action<string>? OnCatalogError;
        
        /// <summary>
        /// Initialize the part catalog system
        /// </summary>
        static PartCatalog()
        {
            _performanceMonitor = PerformanceMonitor.Instance;
            LoadCatalog(_catalogPath);
        }
        
        /// <summary>
        /// Load the part catalog from specified path
        /// </summary>
        public static bool LoadCatalog(string catalogPath)
        {
            var startTime = Time.GetTicksMsec();
            
            try
            {
                GD.Print($"PartCatalog: Loading catalog from {catalogPath}");
                
                if (!ResourceLoader.Exists(catalogPath))
                {
                    var errorMsg = $"PartCatalog: Catalog file not found: {catalogPath}";
                    GD.PrintErr(errorMsg);
                    OnCatalogError?.Invoke(errorMsg);
                    return false;
                }
                
                // Read catalog file
                var file = Godot.FileAccess.Open(catalogPath, Godot.FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    var errorMsg = $"PartCatalog: Failed to open catalog file: {catalogPath}";
                    GD.PrintErr(errorMsg);
                    OnCatalogError?.Invoke(errorMsg);
                    return false;
                }
                
                var jsonContent = file.GetAsText();
                file.Close();
                
                // Parse JSON
                var catalogData = JsonSerializer.Deserialize<CatalogData>(jsonContent, GetJsonOptions());
                if (catalogData == null)
                {
                    var errorMsg = "PartCatalog: Failed to parse catalog JSON";
                    GD.PrintErr(errorMsg);
                    OnCatalogError?.Invoke(errorMsg);
                    return false;
                }
                
                // Validate and load parts
                _partDefinitions.Clear();
                _partsByType.Clear();
                
                var loadedCount = 0;
                var errorCount = 0;
                
                foreach (var partDef in catalogData.Parts)
                {
                    if (ValidatePartDefinition(partDef))
                    {
                        _partDefinitions[partDef.PartId] = partDef;
                        
                        // Index by type
                        if (!_partsByType.ContainsKey(partDef.Type))
                        {
                            _partsByType[partDef.Type] = new List<string>();
                        }
                        _partsByType[partDef.Type].Add(partDef.PartId);
                        
                        loadedCount++;
                    }
                    else
                    {
                        GD.PrintErr($"PartCatalog: Invalid part definition: {partDef.PartId}");
                        errorCount++;
                    }
                }
                
                _catalogPath = catalogPath;
                _catalogLoaded = true;
                
                // Setup hot-reload if in development
                SetupHotReload(catalogPath);
                
                var duration = Time.GetTicksMsec() - startTime;
                if (duration > 10.0) // Target: <10ms for complete catalog reload
                {
                    GD.PrintErr($"PartCatalog: Catalog loading took {duration:F1}ms (target: <10ms)");
                }
                
                GD.Print($"PartCatalog: Loaded {loadedCount} parts ({errorCount} errors) in {duration:F1}ms");
                
                if (_catalogLoaded)
                    OnCatalogReloaded?.Invoke($"Reloaded {loadedCount} parts");
                else
                    OnCatalogLoaded?.Invoke($"Loaded {loadedCount} parts");
                
                return true;
            }
            catch (Exception ex)
            {
                var errorMsg = $"PartCatalog: Exception loading catalog: {ex.Message}";
                GD.PrintErr(errorMsg);
                OnCatalogError?.Invoke(errorMsg);
                return false;
            }
        }
        
        /// <summary>
        /// Reload the catalog (hot-reload support)
        /// </summary>
        public static bool ReloadCatalog()
        {
            var currentTime = Time.GetTicksMsec() / 1000.0;
            if (currentTime - _lastReloadTime < ReloadCooldown)
            {
                return false; // Too soon after last reload
            }
            
            _lastReloadTime = currentTime;
            GD.Print("PartCatalog: Hot-reloading catalog");
            return LoadCatalog(_catalogPath);
        }
        
        /// <summary>
        /// Create a part instance from the catalog
        /// </summary>
        public static Part? CreatePart(string partId)
        {
            var startTime = Time.GetTicksMsec();
            
            try
            {
                if (!_catalogLoaded)
                {
                    GD.PrintErr("PartCatalog: Catalog not loaded");
                    return null;
                }
                
                if (!_partDefinitions.TryGetValue(partId, out var partDef))
                {
                    GD.PrintErr($"PartCatalog: Part not found in catalog: {partId}");
                    return null;
                }
                
                // Create part based on type
                Part? part = partDef.Type switch
                {
                    PartType.Command => new CommandPod(),
                    PartType.FuelTank => new FuelTank(),
                    PartType.Engine => new Engine(),
                    _ => null
                };
                
                if (part == null)
                {
                    GD.PrintErr($"PartCatalog: Unsupported part type: {partDef.Type}");
                    return null;
                }
                
                // Configure part from definition
                ConfigurePartFromDefinition(part, partDef);
                
                var duration = Time.GetTicksMsec() - startTime;
                if (duration > 1.0) // Target: <1ms per part creation
                {
                    GD.PrintErr($"PartCatalog: Part creation took {duration:F1}ms (target: <1ms) for {partId}");
                }
                
                return part;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"PartCatalog: Exception creating part {partId}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Configure a part instance from its definition
        /// </summary>
        private static void ConfigurePartFromDefinition(Part part, PartDefinition definition)
        {
            // Set core properties using reflection-like approach
            var partType = part.GetType();
            
            // Set basic properties
            typeof(Part).GetProperty("PartId")?.SetValue(part, definition.PartId);
            typeof(Part).GetProperty("PartName")?.SetValue(part, definition.Name);
            typeof(Part).GetProperty("Description")?.SetValue(part, definition.Description);
            typeof(Part).GetProperty("Type")?.SetValue(part, definition.Type);
            typeof(Part).GetProperty("DryMass")?.SetValue(part, definition.Mass.DryMass);
            typeof(Part).GetProperty("FuelMass")?.SetValue(part, definition.Mass.FuelMass);
            
            // Configure type-specific properties
            switch (part)
            {
                case CommandPod commandPod when definition.Command != null:
                    ConfigureCommandPod(commandPod, definition.Command);
                    break;
                    
                case FuelTank fuelTank when definition.FuelTank != null:
                    ConfigureFuelTank(fuelTank, definition.FuelTank);
                    break;
                    
                case Engine engine when definition.Engine != null:
                    ConfigureEngine(engine, definition.Engine);
                    break;
            }
        }
        
        /// <summary>
        /// Configure command pod specific properties
        /// </summary>
        private static void ConfigureCommandPod(CommandPod commandPod, CommandPodDefinition definition)
        {
            commandPod.CrewCapacity = definition.CrewCapacity;
            commandPod.ElectricCharge = definition.ElectricCharge;
            commandPod.ElectricChargeMax = definition.ElectricChargeMax;
            commandPod.HasSAS = definition.HasSAS;
        }
        
        /// <summary>
        /// Configure fuel tank specific properties
        /// </summary>
        private static void ConfigureFuelTank(FuelTank fuelTank, FuelTankDefinition definition)
        {
            fuelTank.LiquidFuel = definition.LiquidFuel;
            fuelTank.LiquidFuelMax = definition.LiquidFuelMax;
            fuelTank.Oxidizer = definition.Oxidizer;
            fuelTank.OxidizerMax = definition.OxidizerMax;
            fuelTank.CanCrossfeed = definition.CanCrossfeed;
        }
        
        /// <summary>
        /// Configure engine specific properties
        /// </summary>
        private static void ConfigureEngine(Engine engine, EngineDefinition definition)
        {
            engine.MaxThrust = definition.MaxThrust;
            engine.SpecificImpulse = definition.SpecificImpulse;
            engine.FuelConsumption = definition.FuelConsumption;
            engine.CanGimbal = definition.CanGimbal;
            engine.GimbalRange = definition.GimbalRange;
        }
        
        /// <summary>
        /// Get all part IDs in the catalog
        /// </summary>
        public static IEnumerable<string> GetAllPartIds()
        {
            return _partDefinitions.Keys;
        }
        
        /// <summary>
        /// Get all parts of a specific type
        /// </summary>
        public static IEnumerable<string> GetPartsByType(PartType partType)
        {
            return _partsByType.TryGetValue(partType, out var parts) ? parts : Enumerable.Empty<string>();
        }
        
        /// <summary>
        /// Get part definition by ID
        /// </summary>
        public static PartDefinition? GetPartDefinition(string partId)
        {
            return _partDefinitions.TryGetValue(partId, out var definition) ? definition : null;
        }
        
        /// <summary>
        /// Check if catalog is loaded
        /// </summary>
        public static bool IsCatalogLoaded => _catalogLoaded;
        
        /// <summary>
        /// Get catalog statistics
        /// </summary>
        public static CatalogStats GetCatalogStats()
        {
            return new CatalogStats
            {
                TotalParts = _partDefinitions.Count,
                PartsByType = _partsByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count),
                IsLoaded = _catalogLoaded,
                CatalogPath = _catalogPath
            };
        }
        
        /// <summary>
        /// Validate a part definition
        /// </summary>
        private static bool ValidatePartDefinition(PartDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.PartId))
                return false;
                
            if (string.IsNullOrWhiteSpace(definition.Name))
                return false;
                
            if (definition.Mass.DryMass <= 0)
                return false;
                
            if (!PartTypeInfo.IsImplemented(definition.Type))
                return false;
            
            // Type-specific validation
            switch (definition.Type)
            {
                case PartType.Command when definition.Command == null:
                case PartType.FuelTank when definition.FuelTank == null:
                case PartType.Engine when definition.Engine == null:
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Setup hot-reload file watching
        /// </summary>
        private static void SetupHotReload(string catalogPath)
        {
            // Hot-reload in development builds only
#if DEBUG
            try
            {
                // Convert Godot resource path to filesystem path
                var realPath = ProjectSettings.GlobalizePath(catalogPath);
                var directoryPath = System.IO.Path.GetDirectoryName(realPath);
                var fileName = System.IO.Path.GetFileName(realPath);
                
                if (directoryPath != null && System.IO.Directory.Exists(directoryPath))
                {
                    _fileWatcher?.Dispose();
                    _fileWatcher = new FileSystemWatcher(directoryPath, fileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };
                    
                    _fileWatcher.Changed += OnCatalogFileChanged;
                    GD.Print($"PartCatalog: Hot-reload enabled for {catalogPath}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"PartCatalog: Failed to setup hot-reload: {ex.Message}");
            }
#endif
        }
        
        /// <summary>
        /// Handle catalog file change for hot-reload
        /// </summary>
        private static void OnCatalogFileChanged(object sender, FileSystemEventArgs e)
        {
            // Delay to allow file write to complete
            CallDeferred(nameof(ReloadCatalog));
        }
        
        /// <summary>
        /// Call deferred method (Godot-style deferred call simulation)
        /// </summary>
        private static void CallDeferred(string methodName)
        {
            // In a real implementation, this would use Godot's CallDeferred
            // For now, just call directly after a small delay
            var timer = new Timer
            {
                WaitTime = 0.1f,
                OneShot = true
            };
            
            timer.Timeout += () =>
            {
                if (methodName == nameof(ReloadCatalog))
                    ReloadCatalog();
                timer.QueueFree();
            };
            
            // This would need to be added to the scene tree in actual usage
        }
        
        /// <summary>
        /// Get JSON serialization options
        /// </summary>
        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                Converters = 
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            };
        }
        
        /// <summary>
        /// Cleanup resources
        /// </summary>
        public static void Cleanup()
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;
            _partDefinitions.Clear();
            _partsByType.Clear();
            _catalogLoaded = false;
        }
    }
    
    /// <summary>
    /// Catalog statistics
    /// </summary>
    public struct CatalogStats
    {
        public int TotalParts;
        public Dictionary<PartType, int> PartsByType;
        public bool IsLoaded;
        public string CatalogPath;
    }
    
    /// <summary>
    /// Root catalog data structure
    /// </summary>
    public class CatalogData
    {
        public string Version { get; set; } = "1.0";
        public List<PartDefinition> Parts { get; set; } = new();
    }
    
    /// <summary>
    /// Part definition from JSON catalog
    /// </summary>
    public class PartDefinition
    {
        public string PartId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PartType Type { get; set; } = PartType.Unknown;
        public MassDefinition Mass { get; set; } = new();
        public CommandPodDefinition? Command { get; set; }
        public FuelTankDefinition? FuelTank { get; set; }
        public EngineDefinition? Engine { get; set; }
    }
    
    /// <summary>
    /// Mass properties definition
    /// </summary>
    public class MassDefinition
    {
        public double DryMass { get; set; }
        public double FuelMass { get; set; }
    }
    
    /// <summary>
    /// Command pod specific definition
    /// </summary>
    public class CommandPodDefinition
    {
        public int CrewCapacity { get; set; }
        public double ElectricCharge { get; set; }
        public double ElectricChargeMax { get; set; }
        public bool HasSAS { get; set; }
    }
    
    /// <summary>
    /// Fuel tank specific definition
    /// </summary>
    public class FuelTankDefinition
    {
        public double LiquidFuel { get; set; }
        public double LiquidFuelMax { get; set; }
        public double Oxidizer { get; set; }
        public double OxidizerMax { get; set; }
        public bool CanCrossfeed { get; set; }
    }
    
    /// <summary>
    /// Engine specific definition
    /// </summary>
    public class EngineDefinition
    {
        public double MaxThrust { get; set; }
        public double SpecificImpulse { get; set; }
        public double FuelConsumption { get; set; }
        public bool CanGimbal { get; set; }
        public double GimbalRange { get; set; }
    }
}