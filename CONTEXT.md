# Context: Vowels Energy Management App

**Vowels** is an independent C#-based energy management application (.NET Native AOT) designed to provide high-performance, greedy optimization for home energy systems. It is built to be HACS-compatible.

## Domain Model

### Resource API
The central mechanism for communication between integration modules (PV, Battery, EV, UFH) and the core optimizer.
- **Energy/Power Constraints**: Strongly-typed slot inputs (readonly structs) that define hardware limits.
- **Topology Awareness**: Support for DC-coupled hardware modeling (Solar -> Battery DC flows).

## Glossary

- **Resource API**: The standardized interface for hardware constraints.
- **Greedy Optimizer**: The core engine that allocates energy based on priority and constraints.
- **Topology Constraint**: A limit defined by physical wiring (e.g., inverter max DC input).
- **Vowels**: The name of this energy management ecosystem.
- **Native AOT**: The compilation mode used to produce zero-dependency, high-performance native binaries.
