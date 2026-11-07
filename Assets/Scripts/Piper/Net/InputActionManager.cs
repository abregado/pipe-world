using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    public unsafe class InputActionManager
    {
        public List<BoxStruct<InputAction>> myInputActions = new List<BoxStruct<InputAction>>();
        private int nextSequenceNumber = 1;

        public void add(InputAction inputAction)
        {
            inputAction.sequenceNumber = nextSequenceNumber;
            nextSequenceNumber++;
            myInputActions.Add(new BoxStruct<InputAction>(inputAction));
        }

        public void addPlaceProp(PropPrototypeId prototypeId, Vector3 position, Vector3 eulerRotation)
        {
            add(new PlacePropData()
            {
                propPrototypeId = prototypeId,
                position = position,
                eulerRotation = eulerRotation,
            });
        }

        public static void apply(World world, List<BoxStruct<InputAction>> inputActions)
        {
            foreach (BoxStruct<InputAction> inputAction in inputActions)
            {
                AllocatorContext allocator = new AllocatorContext();
                allocator.data = inputAction.v.allocatorData;

                // Local actions carry a zeroed allocator context; stamping it here keeps prop ids unique per player.
                bool isThisPlayerAction = allocator.data.playerIndex == 0;
                if (isThisPlayerAction)
                {
                    allocator.data = PiperMain.p.game.networkManager.allocatorContextData;
                    inputAction.v.allocatorData = PiperMain.p.game.networkManager.allocatorContextData;
                }

                Debug.Log($"[Piper] apply {inputAction.v.type} #{inputAction.v.sequenceNumber}");

                switch (inputAction.v.type)
                {
                    case InputActionType.PlaceProp:
                    {
                        PlacePropData place = inputAction.v.data.placeProp;
                        spawnProp(world, allocator, place.propPrototypeId, place.position, place.eulerRotation);
                        break;
                    }

                    case InputActionType.PlacePropUsingItem:
                    {
                        PlacePropUsingItemData place = inputAction.v.data.placePropUsingItem;
                        InventoryItemPrototypeId itemToSpend = PropPrototypes.instance[place.propPrototypeId].itemId;
                        Inventory* spendFrom = BuildingInventory.at(world, place.position);
                        if (itemToSpend == InventoryItemPrototypeId.None || spendFrom == null || spendFrom->getCount(itemToSpend) < 1)
                            break;

                        spawnProp(world, allocator, place.propPrototypeId, place.position, place.eulerRotation);
                        spendFrom->adjustCount(itemToSpend, -1);
                        break;
                    }

                    case InputActionType.DeleteProp:
                    {
                        Prop* prop = inputAction.v.data.deleteProp.prop.tryGet(world);
                        if (prop == null) break;
                        PropLogic.deleteProp(world, prop);
                        break;
                    }

                    case InputActionType.MoveProp:
                    {
                        Prop* prop = inputAction.v.data.moveProp.prop.tryGet(world);
                        if (prop == null) break;
                        prop->position = inputAction.v.data.moveProp.newPosition;
                        world.data.dirty = true;
                        break;
                    }

                    case InputActionType.CollectCollectable:
                    {
                        Prop* prop = inputAction.v.data.collectCollectable.prop.tryGet(world);
                        if (prop == null || prop->prototypeId != PropPrototypeId.CollectableGeneric) break;
                        PropLogic.collectCollectable(world, prop);
                        break;
                    }

                    case InputActionType.ToggleOpenable:
                    {
                        Prop* prop = inputAction.v.data.toggleOpenable.prop.tryGet(world);
                        if (prop == null) break;
                        PropLogic.toggleOpenable(world, prop);
                        break;
                    }

                    case InputActionType.TakeFromRefreshable:
                    {
                        Prop* prop = inputAction.v.data.takeFromRefreshable.prop.tryGet(world);
                        if (prop == null || prop->prototypeId != PropPrototypeId.RefreshableBox) break;
                        PropLogic.takeFromRefreshable(world, prop);
                        break;
                    }

                    case InputActionType.PlaceBuilding:
                    case InputActionType.MoveBuildingEdge:
                    case InputActionType.MoveBuildingVertex:
                    case InputActionType.ExtrudeBuildingEdge:
                    case InputActionType.CreateBuildingTri:
                    case InputActionType.SliceBuildingArea:
                    case InputActionType.DeleteBuildingArea:
                    case InputActionType.PlaceBuildingProp:
                    case InputActionType.DeleteBuildingObject:
                    case InputActionType.SetBuildingHeights:
                    case InputActionType.MoveBuildingProp:
                    case InputActionType.SetBuildingWallStyle:
                        BuildingActions.apply(world, allocator, inputAction.v);
                        break;

                    case InputActionType.AdjustInventory:
                    {
                        // A building by id, or the first one when none is named.
                        AdjustInventoryData adjust = inputAction.v.data.adjustInventory;
                        Building* target = adjust.building != 0 ? world.buildings.find(adjust.building) : BuildingInventory.nearest(world, Vector3.zero);
                        if (target == null) break;
                        target->inventory.adjustCount(adjust.item, adjust.amount);
                        world.data.dirty = true;
                        break;
                    }
                }

                if (isThisPlayerAction)
                    PiperMain.p.game.networkManager.allocatorContextData = allocator.data;
            }
        }

        private static void spawnProp(World world, AllocatorContext allocator, PropPrototypeId prototypeId, Vec3f position, Vec3f eulerRotation)
        {
            if (prototypeId == PropPrototypeId.None || prototypeId >= PropPrototypeId.MAX_VALUE || PropPrototypes.instance[prototypeId] == null)
                return;

            Prop* prop = world.get(world.props.allocate(allocator));
            prop->prototypeId = prototypeId;
            prop->position = position;
            prop->eulerRotation = eulerRotation;
            PropLogic.setupProp(world, prop, allocator);
        }
    }
}
