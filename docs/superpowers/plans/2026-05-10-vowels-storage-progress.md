# Vowels Storage V2 Implementation Progress

Following the `subagent-driven-development` workflow.

## Phase 2.1: Storage Stabilization
- [x] **Task 1: Refactor Binary Specifications**
  - [x] Implement `SchemaEntry` with chaining pointers.
  - [x] Update `FileHeader` for "System Entity 0" directory.
  - [x] Verify `Blittable` status.
- [ ] **Task 2: Implement Uniform Directory & Chaining**
  - [ ] Refactor `EntityStore.cs` for unified chaining.
  - [ ] Implement `SwitchSchema` logic.
  - [ ] Implement `GetActiveSchema(DateTime time)`.
  - [ ] Run regression tests.
- [ ] **Task 3: AOT-Safe Regex Filtering**
  - [ ] Create `AttributeFilterService.cs` with `[GeneratedRegex]`.
  - [ ] Integrate with `VowelsConfig.cs`.

## Phase 3: Reactive Pipeline
- [ ] **Task 4: Bidding Pipeline**
  - [ ] Implement `BiddingEngine.cs`.
  - [ ] `HaWebSocketService.cs` integration.
  - [ ] Final integration test.
