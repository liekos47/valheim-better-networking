# Better Networking PC 2.3.5 (Valheim 1.0.x)

`valheim-better-networking-pc` is an unofficial fork of
[Better Networking](https://github.com/CW-Jesse/valheim-betternetworking)
by [CW_Jesse](https://github.com/CW-Jesse), updated to run on **Valheim 1.0**. Built
against the `l-1.0.7` assemblies (network version 39) and verified against `l-1.0.12`,
`l-1.0.14` and `l-1.0.16` (network version 40), which it has run on through each
auto-update. Credit for the original mod goes to CW_Jesse and its contributors; this
repository carries the changes needed to keep it working on the current game version, plus
the portal fixes described below.

**PC only.** "PC" in the name is the platform: this is a BepInEx mod for the PC (Steam)
version of Valheim and for dedicated servers. It cannot be installed on Xbox or
PlayStation, because consoles cannot load mods. It has only been run and measured on Steam
with crossplay off.

Up to 2.3.4 this fork shipped under the original name, as `CW_Jesse.BetterNetworking.dll`.
From 2.3.5 it is **Better Networking PC**, `BetterNetworkingPC.dll`. See
[Compatibility with the old name](#compatibility-with-the-old-name).

No official build claims 1.0 support: upstream 2.3.2 targets 0.217.28, and the 2.3.3 fork
targets 0.221.4.

## What the mod does

Vanilla Valheim caps how fast a machine sends world data to each peer. On a busy server
every player flatlines at the same ceiling, which is what the lag during group play
actually is. The mod raises the send queue (10 KB to 32 KB) and Steam's send rate
(150 KB/s to a 256 KB/s minimum and 1024 KB/s maximum), and adds zstd compression between
machines that both run it.

## Measured results

From one live server between 2026-09-11 and 2026-09-13: 10 distinct players on home
connections, crossplay off, direct Steam sockets. Traffic was captured with `tcpdump` on
the server and binned per player per second; figures are p50 / p90 / max of those
one-second bins, per player, one direction.

**Only compare captures taken with players clustered together.** Valheim only sends a
player what is near them, so a scattered group produces about 1 KB/s each in vanilla too.
Mixing the two states makes the mod look broken when it is not.

### The problem: vanilla clamps every player at one number

Server to each player, no mod, clustered:

| Hardware | Players | p50 | p90 | max |
|---|---|---|---|---|
| i7-9700 | 7 | 37-42 | 43-44 | 43-45 KB/s |
| i9-9900K | 5 | 62-63 | 63-64 | 64-80 KB/s |

Several players stopping dead at the same number is a cap, not demand. Neither machine was
short of anything: CPU 15-30% of one core, 1.5 GB resident, and the uplink peaked at
4.7 Mbit/s on a line proven to carry 121 Mbit/s.

### With the mod on the server

Same measurement, one figure per player in the capture (i7-9700):

| Players | p50 | p90 | max |
|---|---|---|---|
| 4 | 34, 103, 109, 126 | 50, 127, 133, 141 | 54, 147, 172, 180 KB/s |
| 6 | 12-58 | 25-94 | 53-150 KB/s |
| 8 | 5-65 | 29-90 | 51-135 KB/s |

Roughly twice the sustained rate and three times the peak, and the flat clamp is gone:
rates now move with what is happening instead of stopping at one number. Totals through the
server were 705 KB/s in and 281 KB/s out at 8 players, about 2.2 Mbit/s of upload.

Server load did not change meaningfully. Valheim's main thread, the one that saturates
first, sat at 29% of one core with 8 players (35% max) against 8% idle, and memory went
from 1.52 GB to 1.76 GB.

### What limits it next: unmodded clients

With 7-8 players on, the server peaked at 132-136 KB/s per player against a queue allowing
roughly 640 KB/s, so the server had stopped being the constraint. The vanilla clients had
not: each sat at 113-138 KB/s, which is vanilla's 150 KB/s send rate once overhead is
counted. Valheim has the client nearest an area simulate it, so in a group fight one player
uploads every monster's state for everyone and hits that cap. This is why fights can still
lag after a server-side fix.

With the mod on one client and seven still vanilla, on the same server and session:

| Connection | To server | From server |
|---|---|---|
| Modded client | 26-29 KB/s | 15-44 KB/s |
| The 7 vanilla clients | 37-127 KB/s | 2-118 KB/s |

The modded client moved its traffic in roughly a third of the bytes. Compression only
engages between machines that both run the mod, so the benefit grows with each player who
installs it.

### With the mod on every client

Two days later every player had it. One evening, eight distinct players, up to eight on at
once, all eight negotiating compression with the server in both directions:

| | |
|---|---|
| Server main thread | 26% of one core, averaged over 232 minutes |
| Exceptions | 0 |
| Warnings | 0 |
| Disconnects | 15, all players quitting normally |

The server is no longer part of the lag equation at that point. What remains is the
connection of whichever client owns the area a fight happens in, and with the mod on that
client too, its send rate is 256 KB/s to 1 MB/s instead of vanilla's 150 KB/s, compressed.

For comparison, the same weekend we also trialled a server-side simulation mod, which
moves monster and physics simulation off the clients onto the server. It removes the
area-owner problem entirely, but on an i7-9700 it ran the server's single main thread at
70-86% with six to eight players and saturated it in dungeons (about 120 simulated
creatures is where that CPU crosses the 30 fps budget). Better Networking on every client
gave the calmer result on this hardware; the simulation approach only makes sense with a
much faster single core or fewer players.

### Stability over the first 48 hours

| | |
|---|---|
| Player sessions | 46 joins by 10 distinct players, 9 concurrent at peak |
| Session length | 65 minutes median, 298 minutes longest |
| Connections | every join succeeded; all disconnects were `ClosedByPeer`, i.e. players quitting |
| Errors | zero exceptions naming the mod or Harmony, across every restart |
| Game updates | ran through `l-1.0.7` to `l-1.0.12` and then `l-1.0.14` with no rebuild |
| Host reboot | reloaded clean |

## What changed in 2.3.5

* **Steamworks packets are compressed once.** The old patch recompressed the whole send
  queue on every send attempt. A packet Steam refused stays in that queue, so the retry
  compressed it a second time and the receiver got a corrupt packet. Compression now
  happens when a packet is queued in `ZSteamSocket.Send`. The same fault and the same fix
  are described by [SimplifyDave's fork](https://github.com/LabodiDavid/BetterNetworking10).
* **Portal ghost fix** (server or host, on by default, `Portal Ghost Fix`). Valheim 1.0
  changed `ZDO.InternalSetPosition` to update the sector before it stores the new position,
  while `ZDOPeer.ZDOSectorInvalidated` tests the stored position. The server therefore
  checks the place an object just left, so a player standing at a portal is never told that
  whoever walked through it has gone, and keeps a ghost of them. 0.217 stored the position
  first and did not have this. The fix holds the invalidation back until the position is
  stored, so the vanilla check sees the right place. It applies to every object, not only
  players, and works for players without the mod.
* **Fast portal travel** (client, on by default, `Fast Portal Travel`). Vanilla keeps a
  player on the loading screen for 8 seconds after a portal however quickly the destination
  arrives. Now the client reports its new position on arrival instead of up to 2 seconds
  later and sends a marker that the server echoes. A world-data package under 2048 bytes
  after the echo means the server had nothing more to send (`ZDOMan.SendZDOs` never cuts a
  package short below that), as does a second of silence, and the 8 second floor is
  dropped. Vanilla's own check that the destination is loaded still applies. It needs
  2.3.5 on both the server and the travelling player; otherwise portals behave as in
  vanilla. A host already holds the whole world, so its own floor is dropped outright.
* **Wire format unchanged** (compression protocol version 6), so 2.3.5 pairs with 2.3.4 and
  with official 2.3.x releases; server and clients can update separately.
* **Hooks verified against `l-1.0.16`** with `tools/HookCheck`: nothing missing.
* The two portal changes have been checked against the game's IL and shown to apply as
  Harmony patches, but have not yet been measured in play.

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
2. Copy `BetterNetworkingPC.dll` from the [latest release](../../releases/latest)
   into `BepInEx/plugins`, on the server and on each player's game.
3. Delete the old `CW_Jesse.BetterNetworking.dll` if it is there.
4. Restart the server. Config changes are read at startup, so dedicated servers need a
   restart to pick them up.

The DLL is self-contained: ZstdSharp and the compression dictionary are embedded. To
uninstall, delete that one file.

It works on the game client as well as a dedicated server. On a client, `LogOutput.log`
confirms it loaded and that compression negotiated with the server:

```
[Info   :   BepInEx] Loading [Better Networking PC 2.3.5]
[Message:Better Networking PC] Steamworks: k_ESteamNetworkingConfig_SendRateMin: 153600 -> 262144
[Message:Better Networking PC] Steamworks: k_ESteamNetworkingConfig_SendRateMax: 153600 -> 1048576
[Message:Better Networking PC] Compression: Compression to [server]: True
[Message:Better Networking PC] Compression: Compression from [server]: True
```

Defaults are queue 32 KB, send rate 256 KB/s minimum and 1024 KB/s maximum, compression on.
Config lives at `BepInEx/config/CW_Jesse.BetterNetworking.cfg`.

### Compatibility with the old name

Only the visible name and the file name changed. The plugin ID is still
`CW_Jesse.BetterNetworking`, and so are the network message names and the compression
protocol (version 6). That gives three guarantees:

* **Mixed versions play together.** A player on 2.3.4 or an official 2.3.x release under
  the old name still negotiates compression with a 2.3.5 server, and the other way round.
  They only miss fast portal travel, which needs 2.3.5 on both ends.
* **Settings carry over.** The config file keeps its name, so nothing has to be set again.
* **Leaving the old file behind is harmless.** If both DLLs are in `BepInEx/plugins`,
  BepInEx sees one plugin ID twice, loads the newer version and logs
  `Skipping [Better Networking 2.3.4] because a newer version exists`. Deleting the old
  file just keeps the log clean.

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

The output is `CW_Jesse.BetterNetworking/bin/Release/net472/BetterNetworkingPC.dll`.
Builds are not byte-reproducible across different source paths, so a local build will not
match the release checksum.

### After a Valheim update

Valheim servers update themselves, and any update can break a mod that patches game
internals. Not every update does, and HookCheck is how you tell the difference rather than
guessing. Run it against the new assemblies:

```bash
dotnet run --project tools/HookCheck -- path/to/assembly_valheim.dll path/to/com.rlabrecque.steamworks.net.dll
```

It lists every hooked method, constructor and field with its signature, plus the IL
constants the patches depend on. Diff the output against the previous version: anything
reported `MISSING` means do not ship. If a hook is missing, pull the DLL until it is fixed.

Three extra modes, useful for any mod that patches game internals, not just this one:

```bash
--il Type.Method              # dump a method's IL
--check hooks.txt             # check a list of members: "Type.Method" or "field:Type.m_field"
--dump Type                   # list every method and field of a type
--fieldrefs m_localPlayer     # every method that loads a field
--pattern m_localPlayer GetZDOID   # every method where that field load is followed by that call
--findhash 327122920          # which string literal has this StableHashCode (decode "Failed to find rpc method N")
--strrefs discovered          # who uses a string literal, and the call after it (who registers vs invokes an RPC)
--callers IncrementPlayerStat # every method that calls a method of that name
--rpcsigs                     # every RPC_* handler with its signature (match a deserialisation error to its handler)
```

`--check` exits non-zero if anything is missing, so it works in a build script. `--dump` is
for working out what replaced a member that moved. `--fieldrefs` and `--pattern` find code
that assumes a client: `Player.m_localPlayer` is null on a dedicated server, so any
owner-side method that dereferences it breaks under a serverside-simulation mod. That is how
the `Pickable.RPC_Pick` crash in Valheim 1.0 was found, and the pattern scan turned up five
more methods with the same fault.

The `l-1.0.7` to `l-1.0.12` update is the worked example. All 59 hooked members were
present, no signature changed, and the IL the transpilers read was identical except for
the network version constant going from 39 to 40 — which the mod does not touch. Hence no
rebuild, and the same DLL kept running.

`l-1.0.12` to `l-1.0.14` was the same check with an even smaller answer: across every
class the mod touches, the only change was `FejdStartup.PlayIntroCinematic` being renamed
to `TryPlayIntroCinematic`, which the mod does not hook. Signatures and IL otherwise
identical; the first client to connect afterwards negotiated compression both ways.

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
