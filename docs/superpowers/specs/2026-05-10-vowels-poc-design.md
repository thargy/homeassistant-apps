# Vowels POC Design: High-Performance Energy Management Daemon (E-17)

## 1. Overview
Vowels is a high-performance C# .NET 10 Native AOT daemon designed to replace the EMHASS-based energy management system. It provides sub-second responsiveness, natively understands DC-coupled solar/battery topologies, and manages an 18-month (configurable) historical record for forecasting.

## 2. Technical Stack
*   **Runtime**: .NET 10 (Native AOT) for minimal footprint and sub-second execution.
*   **Dependency Injection**: `Microsoft.Extensions.DependencyInjection` using **Source Generation** (Reflection-free).
*   **Reactivity**: Reactive Extensions (System.Reactive) for `IObservable` sensor streams.
*   **Compression**: **Zstandard (Zstd)** for historical archival.
*   **UI**: Lightweight ASP.NET Core / Static UI exposed via **Home Assistant Ingress**.

## 3. Storage Architecture: Paged Memory-Mapped Files (MMF)
To support sub-second writes and zero-latency shared memory access for the UI, Vowels uses a custom paged MMF format for in memory data, which extends to the previous 24hours, the active hour, and future forecasts. The historic data is effectively immutable and will be stored on disc in the archived format, so reboots will require reloading of the archival format to the in-memory format. The archive format is a zstd compressed version of the MMf. The current hour and future forecasts exist in the raw format as they are mutable.

### 3.1 File Layout
*   **Page Size**: 4KB fixed.
*   **Unified Format**: Every file is a collection of 4KB pages. Every entity (including the Directory itself) is represented as a linked chain of pages.
*   **Uniform Directory**: The Directory is "System Entity 0". It uses the same page-chain logic as data entities.
*   **Page Header**: Every page (except Page 0) starts with:
    *   `NextPageID` (4 bytes): Index of the next page in the stream (0 if last).
    *   `DataOffset` (2 bytes): Start of records within the page.

### 3.2 Global File Header (Page 0)
The first page contains the "Source of Truth" registries:
*   **Metadata**: `Magic` (4 bytes), `Version` (2 bytes), `DirtyBit` (1 byte).
*   **Registries**:
    *   **String Table**: A deduplicated pool of all unique strings. Stored as `[Length (2 bytes), UTF8Bytes]`.
    *   **Schema Registry (Chained)**: Definitions of entity layouts. 
        *   Each entity points to its first `SchemaEntry`.
        *   `SchemaEntry`: `[StartTime (8), FirstDataPageID (4), NextSchemaEntryID (4), AttrCount (1), {AttrNameID (4), Type (1)}[]]`.
        *   Switching schemas forces a page break in the data chain, ensuring page purity.
    *   **Entity Directory (System Entity 0)**: Maps `EntityID` to its first schema entry.
        *   Format: `[EntityNameID (4), FirstSchemaEntryID (4)]`.

### 3.3 Record Format & Type System
Records are stored sequentially in entity-specific linked pages.

*   **Record**: `[TimestampOffset (4 bytes), Confidence (1 byte), ValueBlob]`
*   **Metadata Marker**: If `TimestampOffset == 0xFFFFFFFF`, the record is a metadata event:
    *   `[Marker (4), MetaType (1), Payload]`
    *   `MetaType 0x01 (Schema Switch)`: Payload is the `NewSchemaID` (2 bytes).

*   **Supported Types**:
    *   `0x01 (Double)`: 8-byte IEEE float.
    *   `0x02 (Int32)`: 4-byte signed integer.
    *   `0x03 (Boolean)`: 1-byte (0/1).
    *   `0x04 (StringID)`: 4-byte index into the Global String Table.
    *   `0x05 (Blob)`: `[PageID (4), Offset (2), Length (2)]` pointer for complex JSON/nested structures.

### 3.4 Self-Tuning Allocation & Archival
*   Each `EntityHeader` in the MMF stores a `ReservedPagesForNextHour` hint.
*   At the top of each hour, the completed MMF is unmapped, renamed to `/{year}/{month}/{day}/{hour}.vdata`, and compressed using **Zstd**.

## 4. Value Resolution Pipeline
Vowels implements a **Two-Phase Reactive Pipeline** to ensure data quality and stability.

1.  **Phase 1: Bidding (Prediction/Substitution)**
    *   When an entity state changes (or goes `unavailable`), the system notifies integrations registered as `IPredictionProvider`.
    *   Integrations are told the current highest confidence value (and convidence level) and can return a higher confidence value if they are able (lower or equal confidence values are ignored). If the confidence reaches 255, no more integrations are consulted.
    *   **Highest Confidence Wins**: HA Ground Truth is always 255, so bidding won't occur, otherwise the last (hence highest confidence) value will win out.
2.  **Phase 2: Notification (Settled)**
    *   The "Winning" value is committed to the MMF and emitted to integrations registered as `IValueConsumer` via `IObservable<SettledState>`.

## 5. Home Assistant Integration
*   **Connectivity**: Connects to `ws://supervisor/core/websocket` using the `SUPERVISOR_TOKEN`.
*   **Configuration**: Loads entity interest and deadband settings from `/data/options.json` (mapped from the Add-on YAML).
*   **Ingress**: Publishes a UI to the Supervisor-assigned Ingress port.
*   **Recovery**: A background health-check routine scans for data gaps (e.g., during reboots) and performs a "Fetch-then-Impute" recovery using the HA REST API history before falling back to internal predictions.

## 6. Project Structure
*   `src/Vowels.sln`
*   `src/Vowels.Daemon`: Native AOT worker process.
*   `src/Vowels.Core`: Interfaces, Models, and Reactive Core.
*   `src/Vowels.UI`: Ingress-compatible dashboard.

## 7. Success Criteria for POC
1.  Establish authenticated WebSocket connection via Supervisor.
2.  Successfully read and track all entities specified in the Add-on configuration.
3.  Implement the Paged MMF and verify 1-hour archival rollover with Zstd.
4.  Demonstrate the "Bidding" pipeline by simulating a sensor outage and observing a predicted substitution.
5.  Render the collected history in the Ingress UI.
