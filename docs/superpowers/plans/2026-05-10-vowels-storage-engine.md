# Vowels Storage Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a high-performance, reactive time-indexed storage engine and registry for Vowels.

**Architecture:** A decoupled system where `Vowels.Core.Common` defines the reactive contracts, `EntityRegistry` orchestrates live and historical data, and `FileStoreManager` handles multi-file MMF orchestration.

**Tech Stack:** .NET 8 (Native AOT), Reactive Extensions (Rx), Memory Mapped Files.

---

### Phase 1: Vowels.Core.Common Scaffolding

**Files:**
- Create: `vowels/src/Vowels.Core.Common/Vowels.Core.Common.csproj`
- Create: `vowels/src/Vowels.Core.Common/IEntityRequest.cs`
- Create: `vowels/src/Vowels.Core.Common/IHandle.cs`
- Create: `vowels/src/Vowels.Core.Common/IEntityValue.cs`
- Create: `vowels/src/Vowels.Core.Common/IEntityRegistry.cs`
- Create: `vowels/src/Vowels.Core.Common/IStoreRegistry.cs`

- [ ] **Step 1: Create the Common project**
Create the project file targeting `net8.0` with `PublishAot` enabled and add the `System.Reactive` dependency.

- [ ] **Step 2: Define IEntityRequest and IHandle hierarchy**
```csharp
namespace Vowels.Core.Common;

public interface IEntityRequest { }
public interface IHandle : IEntityRequest { string EntityId { get; } }

public record EntityIDRequest(string EntityId) : IEntityRequest;
public record EntitiesRegexRequest(string Pattern) : IEntityRequest;

public record SensorHandle(string EntityId) : IHandle;
public record BinarySensorHandle(string EntityId) : IHandle;
public record SensorAttributeHandle(string EntityId, string AttributeName) : IHandle;
```

- [ ] **Step 3: Define VowelsType and IEntityValue**
Move `VowelsType` from `Core` and define the `EntityValue` record.
```csharp
public enum VowelsType : byte { Double, Int64, Boolean, StringId, Blob, Timestamp }

public record EntityValue(
    IHandle Handle,
    DateTime Timestamp,
    byte Confidence,
    VowelsType Type,
    object Value
);
```

- [ ] **Step 4: Define IEntityRegistry and IStoreRegistry interfaces**
Ensure both share the reactive methods. Add utility overloads to `IEntityRegistry`.

- [ ] **Step 5: Reference Common from Core**
Update `Vowels.Core.csproj` to reference `Vowels.Core.Common`.

- [ ] **Step 6: Commit Phase 1**
```bash
git add vowels/src/Vowels.Core.Common/
git commit -m "feat: scaffold Vowels.Core.Common interfaces"
```

### Phase 2: Refactor EntityRegistry & EntityStore

**Files:**
- Modify: `vowels/src/Vowels.Core/Registry/EntityRegistry.cs`
- Modify: `vowels/src/Vowels.Core/Storage/EntityStore.cs`

- [x] **Step 1: Implement IEntityRegistry in EntityRegistry**
- [x] **Step 2: Transition EntityStore to IStoreRegistry**
- [x] **Step 3: Update existing tests**
- [x] **Step 4: Commit Phase 2**

### Phase 3: FileStoreManager Implementation (Multi-file MMF)

**Files:**
- Create: `vowels/src/Vowels.Core/Storage/FileStoreManager.cs`
- Create: `vowels/src/Vowels.Core/Storage/HourlyMmfFile.cs`

- [ ] **Step 1: Implement HourlyMmfFile**
Create a class that wraps the current `PagedMmfManager` but for a specific hour's filename pattern (`yyyy-MM-dd-HH.vowl`).

- [ ] **Step 2: Implement FileStoreManager Orchestration**
Logic to find, open, and LRU cache the hourly files based on requested `TimeRange`.

- [ ] **Step 3: Implement Reactive Merging**
The `GetValues` implementation that merges streams from multiple `HourlyMmfFile` instances.

- [ ] **Step 4: Implement Background Rotation**
A simple background worker that ensures the "Current" hour file is ready and rotates old ones.

- [ ] **Step 5: Commit Phase 3**
```bash
git commit -m "feat: implement multi-file FileStoreManager"
```

### Phase 4: Plugin Discovery (Option B)

**Files:**
- Create: `vowels/src/Vowels.Core/Plugins/PluginManager.cs`

- [ ] **Step 1: Implement NativeLibrary loading**
Logic to scan a `plugins/` directory and use `NativeLibrary.Load` to find entry points.

- [ ] **Step 2: Define Plugin Entry Point Contract**
A standard C-ABI export that plugins must implement to register themselves with the `EntityRegistry`.

- [ ] **Step 3: Commit Phase 4**
```bash
git commit -m "feat: add Native AOT plugin discovery (Option B)"
```
