# Decision: .NET Version Selection (.NET 10 vs .NET 9)

## Question
Should Vowels use .NET 9 (STS) or .NET 10 (LTS)?

## Options Evaluated

### Option A: .NET 9 (STS)
- **Pros**: None significant for a new project in May 2026.
- **Cons**: 
    - **End of Life**: .NET 9 Standard Term Support (STS) reaches EOL in May 2026.
    - **Technical Debt**: Starting a project on an EOL runtime is high risk.

### Option B: .NET 10 (LTS)
- **Pros**:
    - **Long Term Support**: Supported until November 2028.
    - **Native AOT**: Improved binary size and performance over .NET 9.
    - **Performance**: Enhanced SIMD support and codegen optimizations.
    - **Future-Proof**: Current latest LTS as of May 2026.
- **Cons**: 
    - Requires latest toolchain (already available in Docker build).

## Recommendation
**Selected: .NET 10 (LTS)**

Rationale: .NET 9 is literally going out of support this month. .NET 10 provides a stable, high-performance foundation with first-class Native AOT support, which is critical for our sub-second response goals.

## Consequences
- The development environment and CI must use the .NET 10 SDK.
- We can leverage the latest C# 14/15 features and AOT-optimized libraries.
