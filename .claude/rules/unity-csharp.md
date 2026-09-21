---
paths: ["Assets/_Project/**/*.cs"]
---
# C# rules for this repo
- Namespace `TwoDude.FactoryTwin.<Area>` where Area is Twin, Robot, UI or Data.
- Serialized fields are `[SerializeField] private`; expose read-only properties, never public fields.
- Use `Awake` for self-wiring, `OnEnable`/`OnDisable` for event subscription pairs, `Start` for cross-object reads. Never subscribe without an unsubscribe.
- Avoid per-frame allocations in `Update`: no LINQ, no string concatenation, no `new` for structs that can be cached.
- MQTT publishing is rate-limited per signal (default 10 Hz) and only on change beyond a deadband. The `SignalPublisher` owns this; individual components never call the MQTT client directly.
- Everything that talks to realvirtual goes through the `TwinSignals` facade so realvirtual can be swapped or upgraded without touching gameplay code.
- Every public class gets a one-line `/// <summary>` saying what station or subsystem it belongs to.
