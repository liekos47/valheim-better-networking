# Better Networking 2.3.4 (Valheim 1.0.7)

An unofficial rebuild of [Better Networking](https://github.com/CW-Jesse/valheim-betternetworking)
by [CW_Jesse](https://github.com/CW-Jesse), updated to run on **Valheim 1.0.7** (network
version 39). All credit for the mod goes to CW_Jesse and its contributors; this repository
only carries the changes needed to keep it working on the current game version.

No official build claims 1.0 support: upstream 2.3.2 targets 0.217.28, and the 2.3.3 fork
targets 0.221.4.

## What the mod does

Vanilla Valheim caps how fast a machine sends world data to each peer. On a busy server
every player flatlines at the same ceiling, which is what the lag during group play
actually is. The mod raises the send queue (10 KB to 32 KB) and Steam's send rate
(150 KB/s to a 256 KB/s minimum and 1024 KB/s maximum), and adds zstd compression between
machines that both run it.

Measured on a dedicated server, server-to-player throughput with players clustered:

| Condition | p50 | p90 | max |
|---|---|---|---|
| Before, 5 players | 62-63 KB/s | 63-64 KB/s | 64-80 KB/s |
| After, 4 players | 103-126 KB/s | 127-141 KB/s | 147-180 KB/s |
| After, 8 players | 5-65 KB/s | 29-90 KB/s | 51-135 KB/s |

Roughly twice the sustained rate and three times the peak, and the flat clamp is gone:
rates now vary with what is happening instead of stopping at one number.

**When measuring, only compare captures taken with players clustered together.** Spread-out
players make server-to-player traffic collapse to about 1 KB/s each in vanilla too, because
the server only relays what is near you. A capture taken while everyone is scattered looks
like the mod broke the send path when nothing is wrong.

Compression only works where both ends run the mod, so installing it on players as well as
the server helps more than the server alone.

## What changed in 2.3.4

* **Rebuilt against the Valheim 1.0.7 assemblies** (BepInExPack 5.4.2333).
* **Player limit fix.** `ZNet.RPC_PeerInfo` contains two `ldc.i4.s 10` constants in 1.0.7:
  a status value, and the actual limit compared against `ZNet.GetNrOfPlayers()`. The old
  transpiler replaced both. It now replaces only the one after the `GetNrOfPlayers` call,
  and logs a warning if it does not find exactly one. At the default limit of 10 the old
  behaviour was harmless; above 10 it would have corrupted a status code.
* **Every hook verified against 1.0.7** with `tools/HookCheck`. Nothing the mod hooks or
  reflects on is missing. One signature changed: `ZNet.Shutdown()` became
  `ZNet.Shutdown(bool save)`. The patch binds by name and its postfix takes no arguments,
  so it still applies.
* **Compression protocol unchanged** (version 6), so this build still pairs with players
  running an official 2.3.x release.

## Installing

1. Install [BepInExPack for Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
   5.4.2333 or later.
2. Copy `CW_Jesse.BetterNetworking.dll` from the [latest release](../../releases/latest)
   into `BepInEx/plugins`, on the server and on each player's game.
3. Restart the server. Config changes are read at startup, so dedicated servers need a
   restart to pick them up.

The DLL is self-contained: ZstdSharp and the compression dictionary are embedded. To
uninstall, delete that one file.

Defaults are queue 32 KB, send rate 256 KB/s minimum and 1024 KB/s maximum, compression on.
Config lives at `BepInEx/config/CW_Jesse.BetterNetworking.cfg`.

## Building

Valheim's assemblies are **not** included here; they belong to Iron Gate and are not
redistributable. Copy these out of your own install (`valheim_server_Data/Managed` or
`valheim_Data/Managed`, plus the BepInEx ones) into `libs-Valheim-1.0.7/`:

```
libs-Valheim-1.0.7/
  0Harmony.dll  BepInEx.dll  PlayFab.dll  PlayFabParty.dll
  UnityEngine.dll  UnityEngine.CoreModule.dll  assembly_utils.dll
  dedicated/assembly_valheim.dll
  dedicated/com.rlabrecque.steamworks.net.dll
```

That directory is gitignored. Then:

```bash
dotnet build CW_Jesse.BetterNetworking/CW_Jesse.BetterNetworking.csproj -c Release
```

The output is `CW_Jesse.BetterNetworking/bin/Release/net472/CW_Jesse.BetterNetworking.dll`.
Builds are not byte-reproducible across different source paths, so a local build will not
match the release checksum.

### After a Valheim update

Valheim servers update themselves, and any update can break a mod that patches game
internals. Before trusting a rebuild, run HookCheck against the new assemblies:

```bash
dotnet run --project tools/HookCheck -- path/to/assembly_valheim.dll path/to/com.rlabrecque.steamworks.net.dll
```

It lists every hooked method, constructor and field with its signature, plus the IL
constants the patches depend on. Add `--il Type.Method` to dump a method's IL. Diff the
output against the previous version: anything reported `MISSING` means do not ship. If a
hook is missing, pull the DLL until it is fixed.

Mixed mod versions are safe: two machines that disagree simply skip compression between
them. Do not run another networking mod alongside this one; they conflict.

## Credits

* [CW_Jesse](https://github.com/CW-Jesse) for creating and maintaining Better Networking
* [Joshua Woods (Cheb)](https://github.com/jpw1991) and [William Seligmann (jsza)](https://github.com/jsza)
  for [contributing code](https://github.com/CW-Jesse/valheim-betternetworking/pulls) upstream
* [Oleg Stepanischev](https://github.com/oleg-st) for [ZstdSharp](https://github.com/oleg-st/ZstdSharp)
* [blaxxun](https://github.com/blaxxun-boop/) for [Network](https://github.com/blaxxun-boop/Network/)

## License

MIT, same as the original. See [LICENSE](LICENSE).
