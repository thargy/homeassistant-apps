# Implementer Subagent: Phase 2.1 Task 2

## Task Description
Refactor `EntityStore.cs` to implement the new schema versioning and uniform chaining architecture.

## Context
- **Repository**: `vowels/src/Vowels.Core`
- **Key Files**: `Storage/EntityStore.cs`, `Storage/BinarySpec.cs`, `Storage/IPageManager.cs`
- **Design**: 
  - Directory: A chain of `EntityDescriptor` records.
  - Schema Chain: A chain of `SchemaEntryHeader` + `AttributeDefinition` arrays.
  - Data Chain: Paged state data (not yet fully implemented in `EntityStore`, focus on schema management).

## Requirements
1. **Schema Chaining**:
   - Implement `SwitchSchema(uint entityId, DateTime startTime, VowelsType stateType, ReadOnlySpan<AttributeDefinition> attributes)`.
   - This must:
     - Find the current schema chain head.
     - Find the LAST `SchemaEntry` in the chain.
     - Allocate/find space for a NEW `SchemaEntry`.
     - Update the PREVIOUS `SchemaEntry`'s `NextSchemaEntryPageId` and `NextSchemaEntryOffset` to point to the new entry.
     - Write the new entry.
2. **Schema Lookup**:
   - Implement `GetActiveSchema(uint entityId, DateTime time)`.
   - This must:
     - Traverse the schema chain from the head.
     - Find the entry that covers the requested `time` (i.e., `entry.StartTime <= time` AND (next entry is null OR `nextEntry.StartTime > time`)).
3. **Obsolete Cleanup**:
   - Remove `SchemaRegistry.cs` if its functionality is now subsumed by `EntityStore`.
   - Update `EntityStore.cs` constructor if it was relying on `SchemaRegistry`.

## Verification
- Code must compile.
- Run `dotnet build` in `Vowels.Core`.
- Prepare `StorageTests.cs` for regression testing (you may need to update test cases to match the new API).

## Status Reporting
Report **DONE** when code is ready for review.
