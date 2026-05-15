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
Update `DataSegment` to take `DateTimeOffset anchor` and `TimeSpan duration` in its constructor.

- [ ] **Step 2: Update FileStoreManager**
Refactor to manage a collection of `DataSegment` objects, calculating the correct file based on the `duration` rather than hardcoded 1-hour slots.

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

- [ ] **Step 2: Implement PluginManager Discovery**
Scan `Plugins/` directory, load assemblies, and find classes decorated with `[VowelsPlugin]`.

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

- [ ] **Step 2: Dispatch Config to Plugins**
When `PluginManager` instantiates a plugin, it should look for a matching entry in the config and pass it to the plugin's `Initialize` method or constructor.

- [ ] **Step 3: Commit**
```bash
git add .
git commit -m "feat: support scoped plugin configuration from config.yaml"
```
