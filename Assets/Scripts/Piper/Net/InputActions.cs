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
        PlaceBuilding,
        MoveBuildingEdge,
        MoveBuildingVertex,
        ExtrudeBuildingEdge,
        CreateBuildingTri,
        SliceBuildingArea,
        DeleteBuildingArea,
        PlaceBuildingProp,
        DeleteBuildingObject,
        SetBuildingHeights,
        MoveBuildingProp,
        SetBuildingWallStyle,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InputAction
    {
        public InputActionData data;
        public AllocatorContextData allocatorData;
        public InputActionType type;
        public int sequenceNumber;

        public const int SizeOf = 48;
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
        [FieldOffset(0)] public PlaceBuildingData placeBuilding;
        [FieldOffset(0)] public MoveBuildingEdgeData moveBuildingEdge;
        [FieldOffset(0)] public MoveBuildingVertexData moveBuildingVertex;
        [FieldOffset(0)] public ExtrudeBuildingEdgeData extrudeBuildingEdge;
        [FieldOffset(0)] public CreateBuildingTriData createBuildingTri;
        [FieldOffset(0)] public SliceBuildingAreaData sliceBuildingArea;
        [FieldOffset(0)] public DeleteBuildingAreaData deleteBuildingArea;
        [FieldOffset(0)] public PlaceBuildingPropData placeBuildingProp;
        [FieldOffset(0)] public DeleteBuildingObjectData deleteBuildingObject;
        [FieldOffset(0)] public SetBuildingHeightsData setBuildingHeights;
        [FieldOffset(0)] public MoveBuildingPropData moveBuildingProp;
        [FieldOffset(0)] public SetBuildingWallStyleData setBuildingWallStyle;
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
        // The building whose inventory changes; 0 means the first building (for debug grants).
        public uint building;

        public static implicit operator InputAction(AdjustInventoryData data)
        {
            return new InputAction()
            {
                type = InputActionType.AdjustInventory,
                data = new InputActionData() { adjustInventory = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PlaceBuildingData
    {
        // The floor's centre; its Y is the floor height.
        public Vec3f position;
        public float yaw;
        public float height;
        public float groundY;

        public static implicit operator InputAction(PlaceBuildingData data)
        {
            return new InputAction()
            {
                type = InputActionType.PlaceBuilding,
                data = new InputActionData() { placeBuilding = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MoveBuildingEdgeData
    {
        public uint building;
        public uint edge;
        public float offset;

        public static implicit operator InputAction(MoveBuildingEdgeData data)
        {
            return new InputAction()
            {
                type = InputActionType.MoveBuildingEdge,
                data = new InputActionData() { moveBuildingEdge = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MoveBuildingVertexData
    {
        public uint building;
        public uint vertex;
        public float x;
        public float z;

        public static implicit operator InputAction(MoveBuildingVertexData data)
        {
            return new InputAction()
            {
                type = InputActionType.MoveBuildingVertex,
                data = new InputActionData() { moveBuildingVertex = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ExtrudeBuildingEdgeData
    {
        public uint building;
        public uint edge;
        public float offset;

        public static implicit operator InputAction(ExtrudeBuildingEdgeData data)
        {
            return new InputAction()
            {
                type = InputActionType.ExtrudeBuildingEdge,
                data = new InputActionData() { extrudeBuildingEdge = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CreateBuildingTriData
    {
        public uint building;
        public uint edge;
        public float x;
        public float z;

        public static implicit operator InputAction(CreateBuildingTriData data)
        {
            return new InputAction()
            {
                type = InputActionType.CreateBuildingTri,
                data = new InputActionData() { createBuildingTri = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SliceBuildingAreaData
    {
        public uint building;
        public uint area;
        public float x;
        public float z;

        public static implicit operator InputAction(SliceBuildingAreaData data)
        {
            return new InputAction()
            {
                type = InputActionType.SliceBuildingArea,
                data = new InputActionData() { sliceBuildingArea = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeleteBuildingAreaData
    {
        public uint building;
        public uint area;

        public static implicit operator InputAction(DeleteBuildingAreaData data)
        {
            return new InputAction()
            {
                type = InputActionType.DeleteBuildingArea,
                data = new InputActionData() { deleteBuildingArea = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PlaceBuildingPropData
    {
        public uint building;
        public PropPrototypeId proto;
        public BuildingFace face;
        public uint faceRef;
        public float u;
        public float v;

        public static implicit operator InputAction(PlaceBuildingPropData data)
        {
            return new InputAction()
            {
                type = InputActionType.PlaceBuildingProp,
                data = new InputActionData() { placeBuildingProp = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MoveBuildingPropData
    {
        public uint building;
        public uint obj;
        public BuildingFace face;
        public uint faceRef;
        public float u;
        public float v;

        public static implicit operator InputAction(MoveBuildingPropData data)
        {
            return new InputAction()
            {
                type = InputActionType.MoveBuildingProp,
                data = new InputActionData() { moveBuildingProp = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SetBuildingWallStyleData
    {
        public uint building;
        public uint edge;
        public byte style;

        public static implicit operator InputAction(SetBuildingWallStyleData data)
        {
            return new InputAction()
            {
                type = InputActionType.SetBuildingWallStyle,
                data = new InputActionData() { setBuildingWallStyle = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SetBuildingHeightsData
    {
        public uint building;
        public float floorY;
        public float height;

        public static implicit operator InputAction(SetBuildingHeightsData data)
        {
            return new InputAction()
            {
                type = InputActionType.SetBuildingHeights,
                data = new InputActionData() { setBuildingHeights = data }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeleteBuildingObjectData
    {
        public uint building;
        public uint obj;

        public static implicit operator InputAction(DeleteBuildingObjectData data)
        {
            return new InputAction()
            {
                type = InputActionType.DeleteBuildingObject,
                data = new InputActionData() { deleteBuildingObject = data }
            };
        }
    }
}
