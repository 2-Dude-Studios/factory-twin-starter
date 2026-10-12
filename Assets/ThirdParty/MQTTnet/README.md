# MQTTnet 4.3.7.1207 (vendored)

MIT. Source: https://github.com/dotnet/MQTTnet — NuGet package `MQTTnet 4.3.7.1207`
(published 2024-09-07), `lib/netstandard2.1/MQTTnet.dll`, unmodified.

    nupkg  sha256 = (see git history of this file)
    dll    sha256 = 270511e90285fc14fa78ab4dc112a438b2fafd7467a37bf795fe963400432aea

`MQTTnet.xml` is the API documentation that ships with the package; it only feeds IDE tooltips.

## Why this version and not MQTTnet 5

MQTTnet 5.x ships `net8.0` and `net10.0` assemblies only. Unity 6000.3 runs .NET Standard 2.1 on
Mono/IL2CPP and has no CoreCLR player, so 5.x cannot load. 4.3.7 is the newest release that targets
netstandard2.1, has no further dependencies, and still speaks MQTT 5.0. Revisit when Unity ships a
CoreCLR scripting backend.

## Why not M2MqttUnity (the original plan)

Its bundled protocol library, Eclipse Paho M2Mqtt, was last released in December 2015, is MQTT 3.1.1
only, and carries EPL-1.0 on top of the wrapper's MIT — an extra obligation to hand a client.
See docs/LICENSES.md.
