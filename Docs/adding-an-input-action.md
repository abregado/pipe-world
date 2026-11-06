# Adding an Input Action

An `InputAction` is a player-authorised world-mutating command, such as placing a prop or collecting an item. Every change the player can make to the `World` goes through one, so the host, a joined client and singleplayer all run the same code. See [netcode.md](netcode.md) for how actions travel.

---

## 1. Add the `InputActionType` value

**File:** `Assets/Scripts/Piper/Net/InputActions.cs`

Add an entry to `InputActionType` (a `byte` enum). Add it after the existing values so numbering stays stable between peers running the same build.

## 2. Create the data struct

**File:** `Assets/Scripts/Piper/Net/InputActions.cs`

Add a `[StructLayout(LayoutKind.Sequential)]` struct with blittable fields and an implicit conversion to `InputAction`:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct MyNewActionData
{
    public PropNetworkIdentifier prop;
    public int someValue;

    public static implicit operator InputAction(MyNewActionData data)
    {
        return new InputAction()
        {
            type = InputActionType.MyNewAction,
            data = new InputActionData() { myNewAction = data }
        };
    }
}
```

| Field type | Use for |
|---|---|
| `PropNetworkIdentifier` | A prop, identified by prototype and position |
| `Vec3f` | A position or Euler rotation |
| `PropPrototypeId`, `InventoryItemPrototypeId` | Prototype references |
| `int`, `byte`, `bool` | Scalars |

## 3. Add it to the union

**File:** `Assets/Scripts/Piper/Net/InputActions.cs`, inside `InputActionData`

Add `[FieldOffset(0)] public MyNewActionData myNewAction;`. Every member sits at offset 0; only the member matching `InputAction.type` is valid.

## 4. Handle it in `apply`

**File:** `Assets/Scripts/Piper/Net/InputActionManager.cs`, inside `apply`

Add a `case` that validates and mutates the world. **Resolve `PropNetworkIdentifier` with `tryGet` and `break` on null: a racing action may have removed the prop.** Set `world.data.dirty = true` for any change.

```csharp
case InputActionType.MyNewAction:
{
    Prop* prop = inputAction.v.data.myNewAction.prop.tryGet(world);
    if (prop == null) break;
    PropLogic.doSomething(world, prop);
    break;
}
```

The case runs identically on the server and, as prediction, on the client, so it must depend only on the world and the action. See [netcode.md](netcode.md#prediction).

## 5. Dispatch it

Call `PiperMain.p.game.inputActionManager.add(...)` from the code that triggers it:

```csharp
PiperMain.p.game.inputActionManager.add(new MyNewActionData()
{
    prop = new PropNetworkIdentifier(this),
    someValue = 42,
});
```

## 6. Check the sizes

Run the player. If `InputAction.SizeOf` or `ClientToServerPacket.SizeOf` no longer match, `PiperMain.doAsserts` throws with the real value ([netcode.md](netcode.md#struct-sizes)); update the constant to it.

## 7. Cover it in the self-test

Add the action to [PiperSelfTest.cs](../Assets/Scripts/Piper/Core/PiperSelfTest.cs) and run `PipeWorld.exe -piperSelfTest`.

---

## Checklist

- [ ] `InputActionType` value added
- [ ] Data struct with `[StructLayout(LayoutKind.Sequential)]` and the implicit operator
- [ ] `[FieldOffset(0)]` member added to `InputActionData`
- [ ] `case` added to `InputActionManager.apply`, null-checking every `tryGet`
- [ ] `world.data.dirty = true` set where state changes
- [ ] Dispatched through `inputActionManager.add`
- [ ] Startup size asserts pass
- [ ] Exercised in `PiperSelfTest`
