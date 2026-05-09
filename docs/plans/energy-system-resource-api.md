# Resource API — Energy System Plan

## Context
We are designing the core "Resource API" for Vowels (the Automated Electrical Energy System). According to the architecture concept, Vowels treats all energy sources, loads, and storage components as independent integration modules that plug into a central allocator (the greedy optimizer). This plan formalizes the C# interface that these resources must implement to communicate their capabilities, requirements, predictions, and confidence levels to the central engine.

## Core Design Principles & Decisions

1. **Performance & Types:** We will use `readonly record struct` and `Span<T>` to minimize runtime overhead and ensure a zero-allocation hot path in the control loop.
2. **Confidence:** Represented as a `float` (`0.0` to `1.0`). Integrations that lack granularity can map internal states (e.g., HIGH/LOW) to fixed floats (e.g., 0.9/0.2).
3. **Flexible Slot Horizons:** Integrations are NOT required to conform to the engine's time grid (e.g., 24 hours in 30-min slots). 
   - A battery might return a single slot for the whole day.
   - A PV might return 30-min slots.
   - The central `vowels_engine` is responsible for aggregating and interpolating these slots into the optimizer's uniform grid.
4. **Forecasting Separation:** Integrations only return what they *know* (current real-time state, or explicit scheduled deadlines). If an integration returns incomplete future slots, the engine defers to `vowels_forecaster` to fill the gaps using historical data and ML.
5. **Topology-Aware Capabilities (Solving the AC/DC split):** 
   - A simple `shared: Inverter` string is insufficient for a hybrid inverter (e.g., DC-coupled battery and PV).
   - We will model a **Bus/Topology** system. Resources declare their connection points: `connections: {"AC": "inverter_1_ac", "DC": "inverter_1_dc"}`. The engine calculates restrictions based on flow across these edges, accurately modeling that PV DC can flow to Battery DC without hitting the AC limit.
6. **Energy (Targets) vs Power (Control):** 
   - Energy defines the state bounds and goals over a window (e.g., EV must hit 41.6kWh target). 
   - Power defines the physical flow capabilities and control granularity (e.g., charging at 7.1kW).
   - **Going past target:** The `target` is the primary goal. The `max` is the absolute physical limit. During negative pricing, the optimizer's objective function will naturally seek to consume more power if it reduces overall cost (profit), thus pushing the energy state past `target` up to `max`.

## Data Models

The following C# structure models the capabilities and requirements of a resource using high-performance primitives. 

### The Constraint System
To avoid confusing "Requirements" with "Constraints" (which limit or modify the optimizer's choices), we define explicit `Constraint` classes.

```csharp
1: namespace Vowels.Core.Models;
2: 
3: public abstract record Constraint;
4: 
5: /// <summary>Limits power to match flow on a specific bus (e.g., EV throttling to grid export).</summary>
6: public sealed record MatchFlowConstraint(string TargetBus, string Direction) : Constraint;
7: 
8: /// <summary>Actual power drawn will be a statistical ratio of nominal level (e.g., UFH averaging 60%).</summary>
9: public sealed record ExpectedRatioConstraint(float Ratio, float StdDev) : Constraint;
10: 
11: /// <summary>Control level must be maintained for a duration once activated.</summary>
12: public sealed record DurationConstraint(int DurationMinutes) : Constraint;
13: ```

### The Slot System
These classes represent the energy and power bounds returned by an integration.

```csharp
public readonly record struct EnergyRequirements(
    float Min = 0.0f,
    float? Critical = null,
    float? Reserve = null,
    float? Threshold = null,
    float? Target = null,
    float? Max = null,
    float? Value = null
);

public abstract record ControlMechanism(Constraint? Constraint = null);
public sealed record FixedControl(float Level, Constraint? Constraint = null) : ControlMechanism(Constraint);
public sealed record VariableControl(float Min, float Max, Constraint? Constraint = null) : ControlMechanism(Constraint);

public readonly record struct PowerRequirements(
    float Min,
    float Max,
    float? Value = null,
    float? StdDev = null,
    IReadOnlyList<ControlMechanism>? Control = null
);

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
    Task<IReadOnlyList<Slot>> GetSlotsAsync(CancellationToken ct = default);
    Task ActuateAsync(ControlSignal signal, CancellationToken ct = default);
}
```

## Task Breakdown
1. **[ ] Implement Core Records**
   - Implement the above models in `Vowels.Core.Models`.
   - Add extension methods for `Power` to `Energy` conversion.
2. **[ ] Implement Topology/Bus Abstraction**
   - Design the bus nodes to replace simple string constraints.
   - Ensure the solver can parse `AC_Bus` and `DC_Bus` constraints.
3. **[ ] Define Resource Interface**
   - Formalize the `IResource` interface in `Vowels.Core.Interfaces`.
4. **[ ] Implement Slot Normalizer**
   - Build the engine utility to normalize heterogeneous `Slot` lists into a uniform time grid.
5. **[ ] Implement Mock Resources**
   - C# mock implementations for PV, Battery, EV, UFH, Dishwasher.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Interpolation Complexity | High | Medium | Normalizing variable-length slots with different start/end times into a fixed grid can lead to rounding errors. We need rigorous unit testing on the `SlotInterpolator`. |
| Topology Solver Performance | Medium | High | Adding network topology (AC vs DC busses) to the greedy optimizer increases algorithmic complexity. We must keep the pathfinding simple (e.g., predefined graph templates). |

> [!TIP]
> ## Ready for Implementation
> The conceptual model for data flow is solidified. We are ready to begin writing the foundational C# code for the Models and the Resource Interface. Let me know if you approve this refined plan!

## Examples

Below are the YAML examples mapping to the proposed Dataclasses, demonstrating how different integrations will express their slots.

### PV
```yaml
start: 5/5/2026 10:00:00+01:00 # We should correct DateTimes to UTC automatically
end: 5/5/2026 10:30:00+01:00
# Note no energy constraint as energy is unconstrained.
# equivalent to:
# energy:
#    min: 0
power: # Power import/export
   min: 0 # W - we won't produce less than this - i.e. we can't import power.
   value: 7000 # W - this is our estimated production
   stddev: 2300 #  W -This indicates the value is an estimate, with std. dev 
   max: 21300 # W - we won't/can't produce more than this
```

**C# Representation:**
```csharp
var pvSlot = new Slot(
    Start: new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc),
    End: new DateTime(2026, 5, 5, 10, 30, 0, DateTimeKind.Utc),
    Power: new PowerRequirements(
        Min: 0.0f,
        Value: 7000.0f,
        StdDev: 2300.0f,
        Max: 21300.0f
    )
);
```

### House Battery
```yaml
# Our slot is the full size, as the data doesn't change, so we will only return a single slot, and it doesn't need a start or end
energy:  
   # min: 0 #Wh - this is the minimum battery level - defaults to 0 anyway, can't have negative energy!
   critical: 3500 #Wh
   reserve: 4000 #Wh 
   max: 20000 #Wh - this is the battery capacity
power:
   min: -10000 # W - we can import power to 10kW
   # Note there is no value, this is a control Vowels can use
   max: 10000 # W - we can't supply more than this
   control: # This is a controllable amont (between min/max)
   # equivalent to
   #  variable:
   #    min: -10000
   #    max: 10000
```

**C# Representation:**
```csharp
var batterySlot = new Slot(
    Energy: new EnergyRequirements(
        Critical: 3500.0f,
        Reserve: 4000.0f,
        Max: 20000.0f
    ),
    Power: new PowerRequirements(
        Min: -10000.0f,
        Max: 10000.0f,
        Control: new[] { new VariableControl(Min: -10000.0f, Max: 10000.0f) }
    )
);
```

### EV
```yaml
# no start necessary - it will default to now, this is when the car is plugged in and ready to charge, if the car is away from home/not plugged in then a future start would be returned.
end: 6/5/2026 06:30:00+01:00 # We specify an end as this is when we need to charge the car by, this would impact if the car has targets etc., or, as in this case, when we also directly specify an energy value to hit.
energy:
   # Note min = 0 as usual
   critical: 10000
   reserve: 10400
   threshold: 20800
   target: 41600
   max: 52000
   value: 41600 # Wh - We are specifying a target to hit by the end of the timeslot
power: # Power import/export
   min: -11000 # W - maximum power consumption
   # Note there is no value, this is a control Vowels can use
   max: 0 # W - we can't produce power (well, not for now, some EVs support car to grid!)
   control: # This is a controllable amont (between min/max)
      fixed:
        - 0 # Don't charge
        - 7100 # single phase ECO+
          constraint: grid_export # EV will charge at 7.1kW, unless the grid export level drops below this, in which case it will throttle to match (between 0 and 7.1kW)
        - 11000 # 3-phase FAST
```

**C# Representation:**
```csharp
var evSlot = new Slot(
    End: new DateTime(2026, 5, 6, 6, 30, 0, DateTimeKind.Utc),
    Energy: new EnergyRequirements(
        Critical: 10000.0f,
        Reserve: 10400.0f,
        Threshold: 20800.0f,
        Target: 41600.0f,
        Max: 52000.0f,
        Value: 41600.0f
    ),
    Power: new PowerRequirements(
        Min: -11000.0f,
        Max: 0.0f,
        Control: new ControlMechanism[]
        {
            new FixedControl(0.0f),
            new FixedControl(7100.0f, new MatchFlowConstraint("grid", "export")),
            new FixedControl(11000.0f)
        }
    )
);
```

### UFH
```yaml
# Our slot is the full size, as the data doesn't change, so we will only return a single slot, and it doesn't need a start or end
# There is no energy constraint as the UFH can draw as much power as needed (up to the limit of the power constraint).
power:
   min: -2100 # W - we can import power to 2.1kW
   # Note there is no value, this is a control Vowels can use
   max: 0 # W - we can't produce power
   control: # This is a controllable amont
      fixed:
         - 0
         - 2100
           constraint: # We don't know exactly how much it will use but we can provide a hint
             ratio: 0.6 # We expect it to only use ~60% of 2.1kW - i.e. 1.26kW, 
             stddev: 0.12 # With a standard deviation of 0.12 - i.e. 12%
```

**C# Representation:**
```csharp
var ufhSlot = new Slot(
    Power: new PowerRequirements(
        Min: -2100.0f,
        Max: 0.0f,
        Control: new ControlMechanism[]
        {
            new FixedControl(0.0f),
            new FixedControl(2100.0f, new ExpectedRatioConstraint(0.6f, 0.12f))
        }
    )
);
```

### Dishwasher
```yaml
end: 6/5/2026 06:30:00+01:00 # We specify an end as this is when we need to charge the car by, this would impact if the car has targets etc., or, as in this case, when we also directly specify an energy value to hit.
energy:
   value: 4500 #Wh - We are specifying a target to hit by the end of the timeslot
power: # Power import/export
   min: -2000 # W - how much power the dishwasher is expected to draw when running
   max: 0 
   control: # This is a controllable amont (between min/max)
      fixed:
         - 0 # Don't run the dishwasher
         - 2000 # Run dishwasher at full power
           constraint:
             ratio: 0.3 # We expect it to only use ~30% of 2kW - i.e. 600W, 
             stddev: 0.02
```

**C# Representation:**
```csharp
var dishwasherSlot = new Slot(
    End: new DateTime(2026, 5, 6, 6, 30, 0, DateTimeKind.Utc),
    Energy: new EnergyRequirements(Value: 4500.0f),
    Power: new PowerRequirements(
        Min: -2000.0f,
        Max: 0.0f,
        Control: new ControlMechanism[]
        {
            new FixedControl(0.0f),
            new FixedControl(2000.0f, new ExpectedRatioConstraint(0.3f, 0.02f))
        }
    )
);
```