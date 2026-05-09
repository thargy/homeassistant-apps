# Design Spec: Pivot to C# (.NET Native AOT)

**Date**: 2026-05-09
**Topic**: Language Pivot from Python to C#
**Status**: Approved

## 1. Context & Rationale

The "Vowels" energy management daemon was originally planned in Python. However, given the requirement for **sub-second responsiveness**, **minimal CPU/resource utilization** on a shared home automation host, and the need to manage **18 months of high-resolution historical data**, a compiled language is preferred.

**C# with .NET Native AOT** has been selected as the compromise position. It leverages the user's considerable C# experience while providing a native, low-memory, and high-performance execution profile equivalent to Go or Rust.

## 2. Technical Design

### 2.1 Core Runtime
- **Runtime**: .NET 10 (Native AOT).
- **Compilation**: Single-file, trim-enabled, native binary.
- **Memory Management**: Minimize allocations in the core 1s-15s control loop using `readonly record struct` and `Span<T>`.

### 2.2 Project Structure
- `Vowels.Daemon`: The core real-time engine and background service.
- `Vowels.Core`: Shared models, Resource API contracts, and optimization logic.
- `Vowels.Api`: AOT-compatible Minimal API to serve the web UI and HA Ingress.
- `Vowels.Storage`: High-performance persistence layer (Bespoke binary or SQLite WAL).

### 2.3 Resource API (C# Translation)
The Python dataclass plan is translated into strongly-typed C# records:

```csharp
public readonly record struct Slot(
    PowerRequirements Power,
    EnergyRequirements? Energy = null,
    DateTime? Start = null,
    DateTime? End = null,
    float Confidence = 1.0f
);

public interface IResource
{
    string Id { get; }
    Task<List<Slot>> GetSlotsAsync(CancellationToken ct);
    Task ActuateAsync(ActuationSignal signal, CancellationToken ct);
}
```

### 2.4 Data Tier
- **Hot Path**: In-memory `ArrayPool`-backed circular buffers for the last 24 hours.
- **Cold Path**: Compressed binary storage for 18-month historical trends, optimized for fast range queries.

## 3. Integration & Deployment
- **Home Assistant Add-on**: The C# binary will be packaged in a minimal Alpine-based Docker container.
- **Ingress**: Served via ASP.NET Core Minimal APIs proxied through HA Supervisor.
- **Configuration**: `options.json` from HA mapped to C# `IOptions`.

## 4. Risks & Mitigations
- **Native AOT Limitations**: Some libraries (Reflection-heavy) aren't AOT-compatible. 
    - *Mitigation*: Stick to AOT-friendly libraries like `System.Text.Json` (source-generated) and `Microsoft.Extensions`.
- **Breaking Language Changes**: C# is stable, unlike Zig.
    - *Mitigation*: Use LTS versions of .NET where possible.
