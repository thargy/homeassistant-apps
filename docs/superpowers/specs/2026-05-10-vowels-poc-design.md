# Vowels POC Design: High-Performance Energy Management Daemon (E-17)

## 1. Overview
Vowels is a high-performance C# .NET 10 Native AOT daemon designed to replace the EMHASS-based energy management system. It provides sub-second responsiveness, natively understands DC-coupled solar/battery topologies, and manages an 18-month historical record for forecasting.

## 2. Technical Stack
*   **Runtime**: .NET 10 (Native AOT) for minimal footprint and sub-second execution.
*   **Dependency Injection**: `Microsoft.Extensions.DependencyInjection` using **Source Generation** (Reflection-free).
*   **Reactivity**: Reactive Extensions (System.Reactive) for `IObservable` sensor streams.
*   **Compression**: **Zstandard (Zstd)** for historical archival.
*   **UI**: Lightweight ASP.NET Core / Static UI exposed via **Home Assistant Ingress**.

## 3. Storage Architecture: Paged Memory-Mapped Files (MMF)
To support sub-second writes and zero-latency shared memory access for the UI, Vowels uses a custom paged MMF format for the "Active Hour."

### 3.1 File Layout
*   **Page Size**: 4KB fixed.
*   **Unified Format**: Every file is a collection of 4KB pages.
*   **Dynamic Header**: The entity map itself is stored in a linked-page stream starting at offset 0.
*   **Entity Data**: Each entity is allocated a stream of linked pages.
*   **Record Format**: `[EntityID (2 bytes), TimestampOffset (4 bytes), Confidence (1 byte), ValueType (1 byte), ValueBlob]`.

### 3.2 Self-Tuning Allocation
*   Each `EntityHeader` in the MMF stores a `ReservedPagesForNextHour` hint.
*   On hourly rollover, the system uses the usage statistics from the current hour to "pre-warm" the allocation for the next hour's MMF, minimizing runtime page allocation.

### 3.3 Archival
*   At the top of each hour, the completed MMF is unmapped, renamed to `/{year}/{month}/{day}/{hour}.vdata`, and compressed using **Zstd**.

## 4. Value Resolution Pipeline
Vowels implements a **Two-Phase Reactive Pipeline** to ensure data quality and stability.

1.  **Phase 1: Bidding (Prediction/Substitution)**
    *   When an entity state changes (or goes `unavailable`), the system notifies integrations registered as `IPredictionProvider`.
    *   Integrations have a short "Resolution Window" (50-100ms) to "bid" a value and a confidence byte (0-255).
    *   **Highest Confidence Wins**: The system selects the best bid (HA Ground Truth is always 255).
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
