# EMHASS Historical Implementation & Audit

## Context
This document summarizes the changes, fixes, and limitations of the previous EMHASS-based energy management system implemented in the Home Assistant repository. It serves as historical context for building the custom Vowels C# replacement (E-17).

## Core Implementation Features
1. **Pyscript Consolidation**: The core logic was heavily reliant on Pyscript to consolidate EMHASS forecasts. It implemented an "Actuator-first" state model.
2. **State Protection & Debouncing**: Because EMHASS often changed its state mid-timeslot to represent the *upcoming* state (dropping the current slot's forecast), Pyscript was used to anchor the current slot until the time boundary officially crossed. Extensive debouncing was required for `rest_command.emhass_publish` to prevent rapid, fragmented recalculations.
3. **Data Alignment**: Pyscript was responsible for zipping various prediction arrays (grid, pv, batt, ev, load) together by timestamp to create a unified timeline.

## Key Fixes & Issues Overcome
During its lifecycle, the EMHASS integration required several complex workarounds:
- **Negative Pricing Throttling**: Fixed an issue where battery charging was inappropriately throttled during negative import pricing.
- **Balance Errors**: Corrected energy balance errors related to inverter and curtailment forecasts.
- **Target Memory**: Fixed scenarios where the target grid power forecast was not robustly remembered throughout the active window.
- **Removed Producer Logic**: Phantom-load compensation and 500W quantization were removed from the producer layer for decision-layer consolidation. Fallback-to-raw behaviors were also removed to enforce a stricter data contract.

## Limitations Driving the Vowels Replacement (Why EMHASS Failed Us)
The primary driver for the custom C# Vowels engine is EMHASS's inability to model our specific hardware topology:

1. **AC-Side Bias**: EMHASS treats battery and solar as AC-side entities. It cannot natively model a DC-coupled topology where Solar → Battery charging happens *before* the inverter (and therefore does not count against the 10kW AC limit).
2. **Solar Clipping & Headroom**: Because of the AC bias, EMHASS tends to fill the battery early from cheap overnight grid or early morning solar. It lacks a strategy to preserve battery headroom, resulting in solar clipping at peak times (when DC solar > 10kW AC and the battery is full).
3. **Architectural Disconnect**: As a separate Add-On container communicating via REST API, EMHASS lacks native awareness of the rich, customized sensor environment in Home Assistant (e.g., Zappi phase targets, Renault EV polling latency, Eaton UPS loads).

## Future System Goals (Vowels)
The replacement system (Vowels) must natively handle DC-coupled constraints, intelligently manage battery headroom to prevent clipping, and operate with sub-second responsiveness directly integrating with Home Assistant's state via a high-performance C# Daemon.
