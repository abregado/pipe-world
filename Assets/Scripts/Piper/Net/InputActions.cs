using System.Runtime.InteropServices;

namespace PipeWorld.Piper
{
    public enum InputActionType : byte
    {
        Invalid = 0,
        PlaceProp,
        PlacePropUsingItem,
        DeleteProp,
        MoveProp,
        CollectCollectable,
        ToggleOpenable,
        TakeFromRefreshable,
        AdjustInventory,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InputAction
    {
        public InputActionData data;
        public AllocatorContextData allocatorData;
        public InputActionType type;
        public int sequenceNumber;

        public const int SizeOf = 44;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputActionData
    {
        [FieldOffset(0)] public PlacePropData placeProp;
        [FieldOffset(0)] public PlacePropUsingItemData placePropUsingItem;
        [FieldOffset(0)] public DeletePropData deleteProp;
        [FieldOffset(0)] public MovePropData moveProp;
        [FieldOffset(0)] public CollectCollectableData collectCollectable;
        [FieldOffset(0)] public ToggleOpenableData toggleOpenable;
        [FieldOffset(0)] public TakeFromRefreshableData takeFromRefreshable;
        [FieldOffset(0)] public AdjustInventoryData adjustInventory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PropNetworkIdentifier
    {
        public PropPrototypeId propPrototypeId;
        public Vec3f position;

        public PropNetworkIdentifier(Prop* prop)
        {
            if (prop == null)
            {
                propPrototypeId = PropPrototypeId.None;
                position = Vec3f.INVALID;
            }
            else
            {
                propPrototypeId = prop->prototypeId;
                position = prop->position;
            }
        }

        public PropNetworkIdentifier(PropBehaviour prop)
        {
            if (prop == null)
            {
                propPrototypeId = PropPrototypeId.None;
                position = Vec3f.INVALID;
            }
            else
            {
                propPrototypeId = prop.PropPrototypeId;
                position = prop.transform.position;
            }
        }

        public readonly Prop* tryGet(World world)
        {
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* prop = world.props.slabs.get(i);
                if (!prop->slabHeader.allocated)
                    continue;

                if (prop->prototypeId == propPrototypeId && prop->position.Equals(position))
                    return prop;
            }

            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PlacePropData
    {
        public PropPrototypeId propPrototypeId;
        public Vec3f position;
        public Vec3f eulerRotation;

        public static implicit operator InputAction(PlacePropData data)
        {
            return new InputAction()
            {
                type = InputActionType.PlaceProp,
                data = new InputActionData() { placeProp = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PlacePropUsingItemData
    {
        public PropPrototypeId propPrototypeId;
        public Vec3f position;
        public Vec3f eulerRotation;

        public static implicit operator InputAction(PlacePropUsingItemData data)
        {
            return new InputAction()
            {
                type = InputActionType.PlacePropUsingItem,
                data = new InputActionData() { placePropUsingItem = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeletePropData
    {
        public PropNetworkIdentifier prop;

        public static implicit operator InputAction(DeletePropData data)
        {
            return new InputAction()
            {
                type = InputActionType.DeleteProp,
                data = new InputActionData() { deleteProp = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MovePropData
    {
        public PropNetworkIdentifier prop;
        public Vec3f newPosition;

        public static implicit operator InputAction(MovePropData data)
        {
            return new InputAction()
            {
                type = InputActionType.MoveProp,
                data = new InputActionData() { moveProp = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CollectCollectableData
    {
        public PropNetworkIdentifier prop;

        public static implicit operator InputAction(CollectCollectableData data)
        {
            return new InputAction()
            {
                type = InputActionType.CollectCollectable,
                data = new InputActionData() { collectCollectable = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ToggleOpenableData
    {
        public PropNetworkIdentifier prop;

        public static implicit operator InputAction(ToggleOpenableData data)
        {
            return new InputAction()
            {
                type = InputActionType.ToggleOpenable,
                data = new InputActionData() { toggleOpenable = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TakeFromRefreshableData
    {
        public PropNetworkIdentifier prop;

        public static implicit operator InputAction(TakeFromRefreshableData data)
        {
            return new InputAction()
            {
                type = InputActionType.TakeFromRefreshable,
                data = new InputActionData() { takeFromRefreshable = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AdjustInventoryData
    {
        public InventoryItemPrototypeId item;
        public int amount;

        public static implicit operator InputAction(AdjustInventoryData data)
        {
            return new InputAction()
            {
                type = InputActionType.AdjustInventory,
                data = new InputActionData() { adjustInventory = data }
            };
        }
    }
}
