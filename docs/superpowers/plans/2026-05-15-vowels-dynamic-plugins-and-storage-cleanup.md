# Vowels Dynamic Plugins & Storage Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transition Vowels to a standard .NET 10 JIT runtime with a dynamic, attribute-driven plugin system and generalized storage abstractions.

**Architecture:** 
- **Common:** `Vowels.Common` contains all shared contracts and attributes.
- **Loading:** `Vowels.Daemon` uses `AssemblyLoadContext` to discover and load integrations from a `/Plugins` directory.
- **Storage:** Storage is abstracted into `IDataReader` and `IDataWriter`, with `DataSegment` replacing the rigid `HourlyMmfFile`.
- **Config:** A scoped configuration model where the Daemon dispatches specific `config.yaml` sections to individual plugin instances.

**Tech Stack:** .NET 10, System.Runtime.Loader (ALC), Memory Mapped Files, YamlDotNet.

---

### Phase 1: Core Rename & Infrastructure

#### Task 1: Rename Vowels.Core.Common to Vowels.Common

**Files:**
- Rename: `vowels/src/Vowels.Core.Common/` -> `vowels/src/Vowels.Common/`
- Rename: `Vowels.Core.Common.csproj` -> `Vowels.Common.csproj`
- Modify: `Vowels.sln`
- Modify: All files using `Vowels.Core.Common` namespace

- [ ] **Step 1: Rename the project directory and file**
```bash
mv vowels/src/Vowels.Core.Common/Vowels.Core.Common.csproj vowels/src/Vowels.Core.Common/Vowels.Common.csproj
mv vowels/src/Vowels.Core.Common vowels/src/Vowels.Common
```

- [ ] **Step 2: Update Solution File**
Modify `Vowels.sln` to reflect the new path and name.

- [ ] **Step 3: Global Namespace Update**
Run a global search and replace: `Vowels.Core.Common` -> `Vowels.Common`.

- [ ] **Step 4: Commit**
```bash
git add .
git commit -m "refactor: rename Vowels.Core.Common to Vowels.Common"
```

#### Task 2: Configure Build Artifacts & Native AOT Removal

**Files:**
- Modify: `vowels/src/Vowels.Daemon/Vowels.Daemon.csproj`
- Modify: `vowels/src/Vowels.FileStoreRegistry/Vowels.FileStoreRegistry.csproj`
- Modify: `vowels/Dockerfile`

- [ ] **Step 1: Update Daemon Project**
Remove `PublishAot`, update to `net10.0`, and remove hard-link to `Vowels.FileStoreRegistry`.

```xml
<!-- vowels/src/Vowels.Daemon/Vowels.Daemon.csproj -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <PublishAot>false</PublishAot>
</PropertyGroup>

<ItemGroup>
  <!-- Remove this line -->
  <!-- <ProjectReference Include="..\Vowels.FileStoreRegistry\Vowels.FileStoreRegistry.csproj" /> -->
</ItemGroup>
```

- [ ] **Step 2: Set Plugin Output Path**
Update `Vowels.FileStoreRegistry.csproj` (and future plugins) to build into a central `Plugins` directory for the Daemon.

```xml
<PropertyGroup>
  <OutputPath>..\Vowels.Daemon\bin\$(Configuration)\net10.0\Plugins\$(AssemblyName)\</OutputPath>
  <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
</PropertyGroup>
```

- [ ] **Step 3: Update Dockerfile**
Switch from `alpine` to `.NET 10 Runtime` base and remove AOT build dependencies.

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS final
# ...
```

- [ ] **Step 4: Commit**
```bash
git add .
git commit -m "build: switch to .NET 10 JIT and configure plugin output paths"
```

---

### Phase 2: Storage Abstraction (Reader/Writer)

#### Task 3: Define Data Interfaces

**Files:**
- Create: `vowels/src/Vowels.Common/Storage/IDataReader.cs`
- Create: `vowels/src/Vowels.Common/Storage/IDataWriter.cs`
- Modify: `vowels/src/Vowels.Common/IEntityStore.cs`

- [ ] **Step 1: Create IDataReader**
```csharp
namespace Vowels.Common.Storage;

public interface IDataReader : IDisposable
{
    IEnumerable<string> GetKnownEntityIds();
    IObservable<EntityValue> GetValues(IEnumerable<string> entityIds, DateTimeOffset start, DateTimeOffset end);
}
```

- [ ] **Step 2: Create IDataWriter**
```csharp
namespace Vowels.Common.Storage;

public interface IDataWriter : IDataReader
{
    void SaveValues(IEnumerable<EntityValue> values);
}
```

- [ ] **Step 3: Commit**
```bash
git add .
git commit -m "feat: define IDataReader and IDataWriter interfaces"
```

#### Task 4: Generalize HourlyMmfFile to DataSegment

**Files:**
- Rename: `vowels/src/Vowels.FileStoreRegistry/HourlyMmfFile.cs` -> `vowels/src/Vowels.FileStoreRegistry/DataSegment.cs`
- Modify: `vowels/src/Vowels.FileStoreRegistry/FileStoreManager.cs`

- [ ] **Step 1: Rename and Implement Interfaces**
Rename `HourlyMmfFile.cs` to `DataSegment.cs`.
```bash
mv vowels/src/Vowels.FileStoreRegistry/HourlyMmfFile.cs vowels/src/Vowels.FileStoreRegistry/DataSegment.cs
```
Update `DataSegment` constructor to take `DateTimeOffset anchor` and `TimeSpan duration`.
```csharp
// In vowels/src/Vowels.FileStoreRegistry/DataSegment.cs
using Vowels.Common.Storage;

namespace Vowels.FileStoreRegistry;

internal class DataSegment : IDataWriter, IDisposable
{
    private readonly DateTimeOffset _anchor;
    private readonly TimeSpan _duration;
    // ... other fields remain the same

    public DataSegment(string filePath, DateTimeOffset anchor, TimeSpan duration)
    {
        _anchor = anchor;
        _duration = duration;
        // ... existing init logic
    }

    public IEnumerable<string> GetKnownEntityIds() { /* existing logic */ }
    
    public IObservable<EntityValue> GetValues(IEnumerable<string> entityIds, DateTimeOffset start, DateTimeOffset end)
    {
        // ... existing GetValues logic, but filtering on entityIds instead of IHandle
    }
    
    public void SaveValues(IEnumerable<EntityValue> values)
    {
        foreach (var value in values)
        {
            AddValue(value.Handle.EntityId, value.Type, value.Value, value.Timestamp);
        }
    }
    
    // ... existing AddValue and other logic
}
```

- [ ] **Step 2: Update FileStoreManager**
Refactor to manage a collection of `DataSegment` objects, calculating the correct file based on the `duration` rather than hardcoded 1-hour slots. Update it to implement `IDataReader` and `IDataWriter`.

```csharp
// In vowels/src/Vowels.FileStoreRegistry/FileStoreManager.cs
using Vowels.Common.Storage;

namespace Vowels.FileStoreRegistry;

public class FileStoreManager : IDataWriter, IDisposable
{
    private readonly string _storagePath;
    private readonly TimeSpan _segmentDuration;
    private readonly Dictionary<string, DataSegment> _openSegments = new();
    private readonly object _lock = new();

    public FileStoreManager(string storagePath, TimeSpan segmentDuration)
    {
        _storagePath = storagePath;
        _segmentDuration = segmentDuration;
        if (!Directory.Exists(_storagePath)) Directory.CreateDirectory(_storagePath);
    }

    public IEnumerable<string> GetKnownEntityIds()
    {
        var discovered = new HashSet<string>();
        var files = Directory.GetFiles(_storagePath, "vowels_*.vowl");
        foreach (var filePath in files)
        {
            try
            {
                // In a real scenario we'd parse the anchor from the filename, but for now we just pass dummy values to read the directory.
                using var file = new DataSegment(filePath, DateTimeOffset.MinValue, _segmentDuration);
                foreach (var id in file.GetKnownEntityIds()) discovered.Add(id);
            }
            catch { }
        }
        return discovered;
    }

    public IObservable<EntityValue> GetValues(IEnumerable<string> entityIds, DateTimeOffset start, DateTimeOffset end)
    {
        var observables = new List<IObservable<EntityValue>>();
        // Simple align to segment duration
        long ticks = start.Ticks - (start.Ticks % _segmentDuration.Ticks);
        var current = new DateTimeOffset(ticks, TimeSpan.Zero);
        
        while (current <= end)
        {
            var segment = GetSegmentForTime(current);
            observables.Add(segment.GetValues(entityIds, start, end));
            current = current.Add(_segmentDuration);
        }
        return observables.Concat();
    }

    public void SaveValues(IEnumerable<EntityValue> values)
    {
        // Group values by the segment they belong to
        var grouped = values.GroupBy(v => {
            long ticks = v.Timestamp.Ticks - (v.Timestamp.Ticks % _segmentDuration.Ticks);
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        });

        foreach (var group in grouped)
        {
            var segment = GetSegmentForTime(group.Key);
            segment.SaveValues(group);
        }
    }

    private DataSegment GetSegmentForTime(DateTimeOffset anchor)
    {
        string fileName = $"vowels_{anchor:yyyyMMdd_HHmmss}.vowl";
        string filePath = Path.Combine(_storagePath, fileName);

        lock (_lock)
        {
            if (_openSegments.TryGetValue(filePath, out var segment)) return segment;
            var newSegment = new DataSegment(filePath, anchor, _segmentDuration);
            _openSegments[filePath] = newSegment;
            return newSegment;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var segment in _openSegments.Values) segment.Dispose();
            _openSegments.Clear();
        }
    }
}
```

- [ ] **Step 3: Commit**
```bash
git add .
git commit -m "refactor: generalize HourlyMmfFile to DataSegment"
```

---

### Phase 3: Dynamic Plugin Loading

#### Task 5: Implement VowelsPluginAttribute

**Files:**
- Create: `vowels/src/Vowels.Common/Attributes/VowelsPluginAttribute.cs`

- [ ] **Step 1: Create Attribute**
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class VowelsPluginAttribute : Attribute
{
    public string Name { get; }
    public string Version { get; }
    public bool AllowMultipleInstances { get; init; } = false;

    public VowelsPluginAttribute(string name, string version)
    {
        Name = name;
        Version = version;
    }
}
```

- [ ] **Step 2: Commit**
```bash
git add .
git commit -m "feat: add VowelsPluginAttribute for discovery"
```

#### Task 6: Implement PluginLoader in Daemon

**Files:**
- Create: `vowels/src/Vowels.Daemon/Plugins/PluginLoadContext.cs`
- Create: `vowels/src/Vowels.Daemon/Plugins/PluginManager.cs`

- [ ] **Step 1: Implement AssemblyLoadContext**
Create a `PluginLoadContext` that handles dependency isolation for each plugin folder.
```csharp
// vowels/src/Vowels.Daemon/Plugins/PluginLoadContext.cs
using System.Reflection;
using System.Runtime.Loader;

namespace Vowels.Daemon.Plugins;

public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
```

- [ ] **Step 2: Implement PluginManager Discovery**
Scan `Plugins/` directory, load assemblies, and find classes decorated with `[VowelsPlugin]`.
```csharp
// vowels/src/Vowels.Daemon/Plugins/PluginManager.cs
using System.Reflection;
using Vowels.Common.Attributes;

namespace Vowels.Daemon.Plugins;

public class PluginManager
{
    public IEnumerable<Type> DiscoverPlugins(string pluginsDirectory)
    {
        var pluginTypes = new List<Type>();
        
        if (!Directory.Exists(pluginsDirectory)) return pluginTypes;

        foreach (var pluginDir in Directory.GetDirectories(pluginsDirectory))
        {
            var pluginDlls = Directory.GetFiles(pluginDir, "*.dll");
            
            // Assume the main plugin dll matches the directory name, or just scan all
            foreach (var dll in pluginDlls)
            {
                try
                {
                    var loadContext = new PluginLoadContext(dll);
                    var assembly = loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(dll)));
                    
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.GetCustomAttribute<VowelsPluginAttribute>() != null)
                        {
                            pluginTypes.Add(type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load plugin assembly {dll}: {ex.Message}");
                }
            }
        }
        
        return pluginTypes;
    }
}
```

- [ ] **Step 3: Commit**
```bash
git add .
git commit -m "feat: implement dynamic plugin loading via AssemblyLoadContext"
```

---

### Phase 4: Dynamic Configuration

#### Task 7: Scoped Configuration Mapping

**Files:**
- Modify: `vowels/src/Vowels.Daemon/Config/ConfigLoader.cs`
- Modify: `vowels/src/Vowels.Daemon/Program.cs`

- [ ] **Step 1: Update Config Schema**
Add a `Plugins` section to the master config that maps plugin names to their respective configurations.
```csharp
// This will be part of the object model loaded by YamlDotNet in Daemon
public class VowelsConfig
{
    public Dictionary<string, object> Plugins { get; set; } = new();
}
```

- [ ] **Step 2: Dispatch Config to Plugins**
When `PluginManager` instantiates a plugin, it should look for a matching entry in the config and pass it to the plugin's `Initialize` method or constructor.
*(Implementation details for instantiation are dependent on how plugins declare their interfaces, which is left to the actual plugin implementation).*

- [ ] **Step 3: Commit**
```bash
git add .
git commit -m "feat: support scoped plugin configuration from config.yaml"
```
