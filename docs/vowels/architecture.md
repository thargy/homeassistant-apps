# Vowels — Automated Electrical Energy System Architecture

## Concept
**Vowels** (Automated Electrical Energy System) is our custom, C#-based energy orchestrator (.NET 10 Native AOT). It is designed to replace EMHASS, solve physical non-linear constraints (like DC-coupled solar clipping), and provide a highly resilient, modular, and real-time control loop for the home.

## Core Architectural Paradigm: The Resource API
Vowels is built on a highly modular **Resource API** abstraction. Rather than hardcoding specific devices (Zappi, SolarEdge), the system treats all sources and loads as independent integration modules that plug into a central allocator.

Each resource module specifies its parameters to the central Vowels engine:
- **PV (Source):** "I can provide ~7kWh between 10:00-10:30. My rate is capped by the shared 'Inverter' resource to 10kW. I cannot be given power. Prediction confidence: 80%."
- **Battery (Storage):** "I can accept up to 12kWh at any time, input power capped by shared 'Inverter'. I can output 8kWh at any time, output capped by 'Inverter'. You specify the power flow."
- **EV (Deferrable Load):** "I must have 35kWh by 06:30. You can provide me with 11kW, 7.3kW, or variable excess power."
- **UFH / Immersion (Flexible Load):** "I can accept exactly 2kW at any time. I might refuse if the thermostat reaches target. No energy cap limit."
- **Smart Appliances (E.g. Dishwasher):** "I need 2kWh over 2 hours by 07:00 tomorrow. You can specify my start time, but no other control."

The central Vowels greedy optimizer accepts these profiles, schedules them against dynamic tariffs (Octopus Agile), and actuates them via the API. This ensures future hardware (new cars, new inverters, new appliances) can simply be "plugged in" as a new resource class.

## Goals & Responsibilities
1. **Collate Data:** Collate raw sensor data into a bespoke, high-performance file store independent of Home Assistant.
2. **Granular Forecasting:** Forecast raw sensor data 2 to 7 days ahead, constantly re-evaluating based on real-time inputs.
3. **Real-time Evaluation:** Contrast real-world sensors with forecasts to make high-frequency decisions.
4. **Real-time Granular Control:** Actuate hardware outputs dynamically based on the evaluation delta.

## Resilience Principles (Critical)
1. **Intelligent Fallbacks:** Resilient to cloud API/Modbus outages. Instead of falling back blindly to a laggy CT clamp, Vowels estimates missing values using previous high-fidelity data + current forecast + secondary sensors (e.g., SolarEdge Modbus down -> use previous Modbus trend + Solcast + MyEnergi CT).
2. **Failsafe State:** If confidence drops below safety thresholds, the system fails into **Maximize Self Consumption**, suspending EVs and UFH.
3. **Reboot Resilience:** Must recover cleanly from host reboots and prolonged power outages.
4. **Latency Awareness:** Respects that hardware (Zappi, Renault 5, Inverters) takes time to propagate state changes. Built-in hysteresis prevents command thrashing.
5. **Override Detection:** Explicitly designed to differentiate between a "Vowels automated command" and a "User manual override" (e.g., guest car, forced boost), backing off appropriately.
6. **Smart Notifications:** Pushes mobile phone notifications for critical failures or manual interventions, but stays silent during routine operations.
7. **Bespoke Storage:** Uses its own optimized file-backed memory/storage (JSON/CSV or local SQLite) to prevent HA Recorder/DB bloat and ensure lightning-fast historical queries.
8. **Host Agnostic & Performant:** Execution targets a 1s-15s continuous loop. It will be built as a standalone Home Assistant App (.NET Native AOT) to ensure it runs completely independently in its own container, with minimal CPU and memory overhead.

### 0. `vowels-app` (Docker Container)
- The HA App wrapper. Contains the `config.yaml`, `Dockerfile`, and the compiled .NET Native binary.

### 1. `Vowels.Storage`
- Agnostic to integrations; manages the bespoke high-speed file store mapped to the App's `/data` volume for long-term granular data and confidence indicators. 

### 2. `Vowels.Forecaster`
- Evaluates multi-day horizons. Based *exclusively* on data from storage and real-time inputs.

### 3. `Vowels.RealTime`
- Responsible for querying integrations for real-time state, capabilities, and confidence indicators via WebSocket/REST/Modbus.

### 4. `Vowels.Engine` (The Greedy Optimizer)
- The core loop. Calls `Vowels.RealTime` for updates, updates `Vowels.Storage` with the latest data, and computes the optimal schedule (prioritizing PV headroom).
