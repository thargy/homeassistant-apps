# Vowels POC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a high-performance C# .NET 10 Native AOT daemon that tracks HA entities in a paged MMF storage system and implements a reactive bidding pipeline for predictions.

**Architecture:** A lightweight worker using `CreateSlimBuilder` for AOT compatibility. Data is persisted in 4KB-paged MMFs with global registries for strings and schemas. State resolution uses a two-phase `System.Reactive` pipeline.

**Tech Stack:** .NET 10 (Native AOT), Microsoft.Extensions.DependencyInjection (Source Gen), System.Reactive, System.IO.MemoryMappedFiles, ZstdNet.

---

## File Structure & Responsibilities

### Vowels.Core
- `Storage/BinarySpec.cs`: Consts, Enums, and blittable structs for the MMF layout.
- `Storage/IPageManager.cs`: Interface for page allocation and linking.
- `Storage/StringTable.cs`: Logic for interning strings into the MMF header.
- `Storage/SchemaRegistry.cs`: Logic for managing and retrieving SchemaIDs.
- `Models/VowelState.cs`: The settled state record.
- `Interfaces/IPredictionProvider.cs`: Bidding phase interface.
- `Interfaces/IValueConsumer.cs`: Settled phase interface.

### Vowels.Daemon
- `Services/ConfigLoader.cs`: Loads `config.yaml` and `/data/options.json`.
- `Services/HaWebSocketClient.cs`: Authenticated Supervisor connection.
- `Services/BiddingEngine.cs`: Orchestrates the confidence-based bidding.
- `Services/StorageWorker.cs`: Background task for MMF writes and archival.
- `Program.cs`: AOT-compatible entry point.

### Vowels.UI
- `wwwroot/index.html`: Simple dashboard.
- `Controllers/StateController.cs`: Minimal API to expose MMF data to UI.

---

## Phase 1: Scaffolding & Configuration

### Task 1: Initialize .NET 10 Solution
- [ ] **Step 1: Create solution and projects**
Run:
```bash
dotnet new sln -n Vowels
dotnet new console -n Vowels.Daemon --use-program-main
dotnet new classlib -n Vowels.Core
dotnet new webapi -n Vowels.UI --use-program-main
dotnet sln add Vowels.Daemon Vowels.Core Vowels.UI
```
- [ ] **Step 2: Configure Native AOT**
Modify `Vowels.Daemon/Vowels.Daemon.csproj`:
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <OptimizationPreference>Speed</OptimizationPreference>
</PropertyGroup>
```
- [ ] **Step 3: Commit**
`git add . && git commit -m "chore: scaffold .NET 10 AOT solution"`

### Task 2: Implement Config Loader
- [ ] **Step 1: Write tests for YAML/JSON merging**
File: `Vowels.Core.Tests/ConfigTests.cs`
- [ ] **Step 2: Implement ConfigLoader using SourceGen JSON**
File: `Vowels.Daemon/Services/ConfigLoader.cs`
- [ ] **Step 3: Commit**
`git commit -m "feat: implement AOT-compatible config loading"`

---

## Phase 2: MMF Storage Engine (The "Heart")

### Task 3: Binary Specification & Page Manager
- [ ] **Step 1: Define blittable structs for Header and PageHeader**
File: `Vowels.Core/Storage/BinarySpec.cs`
- [ ] **Step 2: Implement PagedMmfManager with basic page allocation**
- [ ] **Step 3: Write tests for page linking and data offset calculation**
- [ ] **Step 4: Commit**
`git commit -m "feat: implement paged MMF manager and binary specs"`

### Task 4: String Table & Schema Registry
- [ ] **Step 1: Implement StringTable with ID-based lookup**
- [ ] **Step 2: Implement SchemaRegistry for layout definitions**
- [ ] **Step 3: Test schema switching via Metadata Marker (0xFFFFFFFF)**
- [ ] **Step 4: Commit**
`git commit -m "feat: implement string interning and schema registry"`

---

## Phase 3: Reactive Pipeline & HA Integration

### Task 5: Supervisor WebSocket Client
- [ ] **Step 1: Implement WS client using ClientWebSocket**
- [ ] **Step 2: Handle SUPERVISOR_TOKEN authentication**
- [ ] **Step 3: Map incoming events to IObservable streams**
- [ ] **Step 4: Commit**
`git commit -m "feat: implement supervisor websocket integration"`

### Task 6: The Bidding Engine
- [ ] **Step 1: Implement BiddingEngine confidence-check logic**
- [ ] **Step 2: Write tests for "Ground Truth (255) wins instantly"**
- [ ] **Step 3: Write tests for "Highest confidence wins" sequence**
- [ ] **Step 4: Commit**
`git commit -m "feat: implement reactive bidding pipeline"`

---

## Phase 4: UI & Archival

### Task 7: Hourly Archival & Zstd
- [ ] **Step 1: Implement HourlyRollover logic**
- [ ] **Step 2: Integrate ZstdNet for compression**
- [ ] **Step 3: Commit**
`git commit -m "feat: implement hourly archival and zstd compression"`

### Task 8: Ingress Dashboard
- [ ] **Step 1: Create minimal Web UI in Vowels.UI**
- [ ] **Step 2: Map UI to Supervisor Ingress port**
- [ ] **Step 3: Final POC verification**
- [ ] **Step 4: Commit**
`git commit -m "feat: add ingress dashboard and complete POC"`
