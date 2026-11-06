# Adding a Prop

A prop needs an enum value, a prototype, initial data, a behaviour class and a prefab, and the startup checks fail loudly if any are out of step. For how props are stored and shown, see [props-and-inventory.md](props-and-inventory.md).

---

## 1. Add the id

**File:** `Assets/Scripts/Piper/Systems/PropPrototypes.cs`

Add a unique value to `PropPrototypeId` (a `byte`, below `MAX_VALUE`), add it to `PropPrototypes.allIds`, and add a `new Builder(PropPrototypeId.MyProp)` entry to the `temp` array in the `PropPrototypes` constructor with its name, description and, if players place it from an item, `.item(...)`.

## 2. Add per-instance data

**File:** `Assets/Scripts/Piper/Systems/SystemTypes.cs`

If the prop has state, add a struct and a `[FieldOffset(0)]` member on `PropExtraData`. **The union is saved and sent over the network, so changing it changes the layout: bump `Constants.saveVersion`.**

## 3. Add initial data and logic

**File:** `Assets/Scripts/Piper/Systems/PropLogic.cs`

Add a case to `setupProp`, and to `update` if the prop changes over time. Anything the prop does must be a pure function of the world: see [netcode.md](netcode.md#prediction).

## 4. Create the behaviour

**Directory:** `Assets/Scripts/Piper/Props/`

Create `MyPropBehaviour.cs` deriving from `PropBehaviour`, passing the id to the base constructor. **The file name must match the class name, or Unity cannot attach it to a prefab.** Implement `IInteractable` if the player can use it, dispatching an [input action](adding-an-input-action.md) from `Interact`.

## 5. Build the prefab

**File:** `Assets/Editor/PiperSetup.cs`

Add a `BuildMyProp` method called from `BuildPropPrefabs`. Build the mesh with `AddBox` and `SaveMesh`, give the root the behaviour and a collider, and save it with `SavePrefab` to `Assets/Resources/PropPrefabs/MyProp.prefab`. The prefab root sits at the origin with no rotation.

## 6. Regenerate and check

Run `.\build.ps1 -Setup`, then `PipeWorld.exe -piperSelfTest` ([piper-systems.md](piper-systems.md#command-line-flags)).

---

## Checklist

- [ ] `PropPrototypeId` value, `allIds` entry and `Builder` entry
- [ ] `PropExtraData` member added and `saveVersion` bumped, if the prop has state
- [ ] `setupProp` (and `update`) cases
- [ ] `MyPropBehaviour.cs`, file name equal to class name
- [ ] Prefab method in `PiperSetup.cs`
- [ ] `-Setup` run; startup asserts pass
