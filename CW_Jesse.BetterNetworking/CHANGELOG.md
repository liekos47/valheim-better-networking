# Changelog

## 2.3.5

First release as Better Networking PC. PC (Steam) and dedicated servers only. Earlier versions of this fork were shared privately
under the original name.

- Renamed to Better Networking PC (`BetterNetworkingPC.dll`). The plugin ID, config file,
  network message names and compression protocol are unchanged, so it pairs with Better
  Networking 2.3.x and keeps existing settings.
- Steamworks packets are compressed once. A packet Steam refused to send used to be
  compressed again on the retry and arrived corrupt.
- Portal ghost fix (server or host): players no longer stay visible where they left
  through a portal. Works for players without the mod.
- Fast portal travel: leave the portal loading screen as soon as the destination has
  arrived instead of always waiting 8 seconds. Needs 2.3.5 on the server and the player.
- Verified against Valheim 1.0.16.

## 2.3.4

- Rebuilt for Valheim 1.0.
- Player limit above 10 no longer overwrites an unrelated status code.
