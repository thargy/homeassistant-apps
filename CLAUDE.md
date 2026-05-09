# CLAUDE.md: Vowels Energy Management

Project-specific instructions for the Vowels Automated Electrical Energy System.

## 🛠️ Commands
- **Build Addon**: `docker build -t vowels ./vowels`
- **Lint Python**: `ruff check .`
- **Format Code**: `ruff format .`
- **Run Tests**: `pytest`
- **Type Check**: `mypy .`

## 📐 Architecture & Conventions
- **Domain**: Home Energy Optimization (HACS-compatible).
- **Core Pattern**: Resource API (Heterogeneous constraints).
- **Topology Awareness**: Must handle DC-coupled hardware constraints (e.g. inverter clipping).
- **Error Handling**: Use intelligent fallbacks for sensor outages.
- **Style**: Standard Python 3.12 (typing, dataclasses).

## 🗃️ Documentation
- **Context**: `CONTEXT.md`
- **Architecture**: `docs/vowels/architecture.md`
- **Design Spec**: `docs/plans/energy-system-resource-api.md`
