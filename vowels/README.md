# Vowels

**Automated Electrical Energy System**

A highly resilient, native Python-based energy orchestrator that solves non-linear constraints and manages deferrable loads via a Resource API. It is designed to act as a Home Assistant App, providing sub-minute control resolution without impacting the Home Assistant Core event loop.

## Key Features

* **Resource API Paradigm**: Agnostic control over EV Chargers, Batteries, and Solar arrays.
* **Greedy Optimizer**: Real-time evaluation of PV headroom to actuate deferrable loads.
* **Intelligent Fallbacks**: Robust handling of Modbus timeouts or API outages.
* **Isolated Execution**: Runs entirely in its own Docker container with bespoke file-backed historical storage.

For detailed installation and usage instructions, see [DOCS.md](DOCS.md).
