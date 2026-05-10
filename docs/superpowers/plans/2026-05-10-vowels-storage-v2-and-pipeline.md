# Vowels Storage V2 & Pipeline Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Refactor the storage engine to a uniform, growable paged system with schema-chaining and AOT-safe regex filtering.

---

## Phase 2.1: Storage Stabilization (The "Uniform" Refactor)

### Task 1: Refactor Binary Specifications
- [ ] **Step 1: Update BinarySpec.cs structs**
  - [ ] Implement `SchemaEntry` with `StartTime`, `FirstDataPageId`, and `NextSchemaEntryId`.
  - [ ] Update `FileHeader` to treat the directory as "System Entity 0".
  - [ ] Ensure all structs remain `Blittable`.
- [ ] **Step 2: Commit**
  `git add . && git commit -m "refactor: update binary structs for schema chaining"`

### Task 2: Implement Uniform Directory & Chaining
- [ ] **Step 1: Refactor EntityStore.cs**
  - [ ] Unify directory and data page logic (everything is a chain).
  - [ ] Implement `SwitchSchema` logic: trigger page break and link to new `SchemaEntry`.
  - [ ] Implement `GetActiveSchema(DateTime time)` lookup.
- [ ] **Step 2: Regression Testing**
  - [ ] Run `Vowels.Core.Tests/StorageTests.cs`.
- [ ] **Step 3: Commit**
  `git commit -m "feat: implement uniform directory and schema chaining logic"`

### Task 3: AOT-Safe Regex Filtering
- [ ] **Step 1: Create AttributeFilterService.cs**
  - [ ] Use `[GeneratedRegex]` for entity/attribute pattern matching.
  - [ ] Ensure no `RegexOptions.Compiled` usage (AOT restriction).
- [ ] **Step 2: Integrate with VowelsConfig**
  - [ ] Update `VowelsConfig.cs` to use the filter for attribute inclusion.
- [ ] **Step 3: Commit**
  `git commit -m "feat: add AOT-compatible regex attribute filtering"`

---

## Phase 3: Reactive Pipeline Implementation

### Task 4: Bidding Pipeline (The "Confidence" Loop)
- [ ] **Step 1: Implement BiddingEngine.cs**
  - [ ] Orchestrate `IPredictionProvider` calls.
  - [ ] Logic: Highest confidence wins; 255 (Ground Truth) is absolute.
- [ ] **Step 2: Implement HaWebSocketService.cs**
  - [ ] Connect to HA, ingest state changes, and pipe into the Bidding Engine.
- [ ] **Step 3: Integration Test**
  - [ ] Verify HA event -> Bidding -> EntityStore write sequence.
- [ ] **Step 4: Commit**
  `git commit -m "feat: finalize reactive bidding and HA ingestion pipeline"`
