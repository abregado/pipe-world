# Netcode

How the game state is shared between a host and its clients: world mutations travel as `InputAction` commands, the world is sent back as snapshots, and player poses ride along unreliably. Read this before changing anything under `Assets/Scripts/Piper/Net/`.

---

## Transport

Transport is GameNetworkingSockets (`Assets/Plugins/GameNetworkingSockets/`), reached through [GNSNetworkBackend.cs](../Assets/Scripts/Piper/Net/GNSNetworkBackend.cs) and the forwarding layer in [NetworkingBackend.cs](../Assets/Scripts/Piper/Net/NetworkingBackend.cs). A connection has two lanes: lane 0 for unreliable traffic and lane 1 for reliable world snapshots. The server listens on UDP port `Constants.DEFAULT_PORT` (27020). A join address is `host` or `host:port`, with an optional `ip://` prefix.

The native DLLs load from the player's plugin folder; in the editor `GNSNetworkBackend.init` points the loader at `Assets/Plugins/GameNetworkingSockets/Plugins`. There is no authentication: the server accepts every connection and `ClientToServerPacketAuth` is sent with an empty secret.

---

## Network Managers

[NetworkManager.cs](../Assets/Scripts/Piper/Net/NetworkManager.cs) is the abstract base; `PiperGame.networkManager` holds exactly one.

| Class | Role |
|---|---|
| `SingleplayerNetworkManager` | Applies the local actions and ticks the world |
| `Server` | Merges its own and every client's actions, applies them, ticks the world, snapshots it and sends it |
| `Client` | Sends its actions, receives snapshots, and replays unacknowledged actions on top of each one |

Each applies `InputActionManager.apply` and then `world.update()` once per tick, so the same code path runs everywhere.

---

## Two Kinds of Data

| | Player poses | World mutations |
|---|---|---|
| Carried by | `PlayerData` fields in the packets | `InputAction` commands |
| Lane | Unreliable | Commands: unreliable with resend; snapshots: reliable |
| Applied as | Latest value wins | A deterministic switch in `InputActionManager.apply` |

[OtherPlayerSyncer.cs](../Assets/Scripts/Piper/Core/OtherPlayerSyncer.cs) reads `world.data.players` each tick and keeps one avatar per other online player; [OtherPlayerBehaviour.cs](../Assets/Scripts/Piper/Core/OtherPlayerBehaviour.cs) eases the avatar toward the latest pose. Poses are client-authoritative; the server relays them without validation.

---

## Commands

A client puts every action it creates in two lists, not yet received and not yet processed by the server. Each outgoing [ClientToServerPacket](../Assets/Scripts/Piper/Net/NetworkManager.cs) carries up to `MAX_INPUT_ACTIONS` (15) of the unreceived ones, and the client sends one every 8 ms once it has loaded a world. **Reliability is application-level: the server drops any action whose `sequenceNumber` is not above the last it accepted, and acknowledges that number in every `ServerToClientPacket`, which lets the client prune its list.**

The server sends `ServerToClientPacket` roughly every 32 ms with the pose of every player and who is online.

---

## Snapshots

The server serialises the world with `World.save()` when it has changed, at most once a second while a client is behind and every two seconds with no clients. A snapshot is sent as a diff against the last snapshot the client acknowledged ([WorldDiff.cs](../Assets/Scripts/Piper/Net/WorldDiff.cs)): content-defined chunks ([ContentDefinedChunker.cs](../Assets/Scripts/Piper/Net/ContentDefinedChunker.cs)) hashed with xxHash64 are either copied from the anchor or sent literally, with a CRC32 of the result. With no anchor the whole world is sent literally. Diffs are split into `PacketChunk`s of at most 1450 bytes and reassembled by [ReassemblyBuffer.cs](../Assets/Scripts/Piper/Net/ReassemblyBuffer.cs).

Each chunk header carries `lastActionProcessedByServerIndex`, the highest action of that client baked into the snapshot. A client that is more than 25 seconds behind is dropped.

---

## Prediction

The client applies its own actions immediately. When a snapshot arrives it replaces the local world with `World.load`, then re-applies every action the snapshot had not yet baked in. `InputActionManager.apply` is deterministic, so this reproduces the pre-snapshot local state and needs no smoothing.

**Anything an action does must be a pure function of the world and the action.** [PropLogic.cs](../Assets/Scripts/Piper/Systems/PropLogic.cs) derives a collectable's item from its position with an explicit integer hash, because `System.HashCode` is seeded per process and would give the server and client different answers.

---

## Allocator Context

Props get ids from the placing player's `AllocatorContextData` (player index plus a counter), so two players placing at once never collide. `InputActionManager.apply` stamps a local action with the manager's context and **writes the advanced counter back after the action**; without the write-back every placement reuses an id and the slab allocator asserts. `NetworkManager.loadAllocatorStateIfNeeded` finds the local player's slot and resumes the counter after the highest existing id.

---

## Struct Sizes

`PlayerData`, `BasicPlayerData`, `InputAction` and `ClientToServerPacket` are blittable and copied with raw pointers, with hand-written `SizeOf` constants. `PiperMain.doAsserts` compares each with `sizeof` at startup, and the packet must stay at or under 1450 bytes. After changing a field, take the asserted `sizeof` value as the source of truth.

| Struct | `SizeOf` |
|---|---|
| `BasicPlayerData` | 44 |
| `PlayerData` | 108 |
| `InputAction` | 44 |
| `ClientToServerPacket` | 784 |

`InputAction.SizeOf` follows the largest member of the `InputActionData` union plus the 8-byte allocator context, the type byte and the sequence number.

---

## Testing Two Players

Two instances on one machine need distinct player ids, because both read the same settings file. [screenshots.md](screenshots.md) describes the flags; a host and a joining client are started with

```powershell
PipeWorld.exe -piperHost -piperName Host -piperPlayerId 11111111-1111-1111-1111-111111111111
PipeWorld.exe -piperJoin 127.0.0.1 -piperName Client -piperPlayerId 22222222-2222-2222-2222-222222222222
```

The host keeps running while unfocused (`Application.runInBackground`), which two windows on one desktop require.
