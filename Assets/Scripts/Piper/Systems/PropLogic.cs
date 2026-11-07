namespace PipeWorld.Piper
{
    public static unsafe class PropLogic
    {
        private static readonly InventoryItemPrototypeId[] collectableItems =
        {
            InventoryItemPrototypeId.Rock,
            InventoryItemPrototypeId.Metal,
            InventoryItemPrototypeId.Flower,
            InventoryItemPrototypeId.Clay,
        };

        public static void setupProp(World world, Prop* prop, AllocatorContext allocator)
        {
            switch (prop->prototypeId)
            {
                case PropPrototypeId.CollectableGeneric:
                {
                    // Derived from the position so every peer applying this action picks the same item.
                    // System.HashCode is seeded per process, so the hash is spelled out.
                    uint hash = unchecked((uint)((int)(prop->position.x * 100f) * 73856093 ^ (int)(prop->position.z * 100f) * 19349663));
                    prop->data.collectable.givenItem = collectableItems[hash % (uint)collectableItems.Length];
                    prop->data.collectable.amount = 1 + (int)(hash / 7 % 3);
                    break;
                }
                case PropPrototypeId.DoorSingleItem:
                    prop->data.door.doorOpen = false;
                    prop->data.door.requiredItem = InventoryItemPrototypeId.None;
                    break;
                case PropPrototypeId.PlaceableLamp:
                    prop->data.lamp.on = true;
                    break;
                case PropPrototypeId.RefreshableBox:
                    prop->data.refreshable.givenItem = InventoryItemPrototypeId.Obtainium;
                    prop->data.refreshable.maxStored = Constants.refreshableMaxStored;
                    prop->data.refreshable.amountStored = Constants.refreshableMaxStored;
                    prop->data.refreshable.nextRefreshTick = ulong.MaxValue;
                    break;
            }

            world.data.dirty = true;
        }

        public static void update(World world)
        {
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* prop = world.props.slabs.get(i);
                if (!prop->slabHeader.allocated)
                    continue;

                if (prop->prototypeId == PropPrototypeId.RefreshableBox)
                    updateRefreshable(world, prop);
            }
        }

        // nextRefreshTick is ulong.MaxValue while full, not 0: 0 is a valid tick and would refill instantly.
        private static void updateRefreshable(World world, Prop* prop)
        {
            ref RefreshableItemExtraData box = ref prop->data.refreshable;
            if (box.amountStored >= box.maxStored)
            {
                box.nextRefreshTick = ulong.MaxValue;
                return;
            }

            if (box.nextRefreshTick == ulong.MaxValue)
                box.nextRefreshTick = world.data.currentTick + Constants.refreshableTicksPerItem;

            if (world.data.currentTick >= box.nextRefreshTick)
            {
                box.amountStored++;
                box.nextRefreshTick = world.data.currentTick + Constants.refreshableTicksPerItem;
                world.data.dirty = true;
            }
        }

        public static void deleteProp(World world, Prop* prop)
        {
            world.free(prop->getHandleToSelf());
            world.data.dirty = true;
        }

        public static void collectCollectable(World world, Prop* prop)
        {
            // Nothing can be kept without a building to keep it in, so the item stays where it is.
            Inventory* inventory = BuildingInventory.at(world, prop->position);
            if (inventory == null)
                return;

            inventory->adjustCount(prop->data.collectable.givenItem, prop->data.collectable.amount);
            deleteProp(world, prop);
        }

        public static void takeFromRefreshable(World world, Prop* prop)
        {
            ref RefreshableItemExtraData box = ref prop->data.refreshable;
            Inventory* inventory = BuildingInventory.at(world, prop->position);
            if (box.amountStored <= 0 || inventory == null)
                return;

            inventory->adjustCount(box.givenItem, box.amountStored);
            box.amountStored = 0;
            box.nextRefreshTick = ulong.MaxValue;
            world.data.dirty = true;
        }

        public static void toggleOpenable(World world, Prop* prop)
        {
            switch (prop->prototypeId)
            {
                case PropPrototypeId.DoorSingleItem:
                {
                    ref DoorExtraData door = ref prop->data.door;
                    if (!door.doorOpen && door.requiredItem != InventoryItemPrototypeId.None)
                    {
                        Inventory* inventory = BuildingInventory.at(world, prop->position);
                        if (inventory == null || inventory->getCount(door.requiredItem) < 1)
                            return;
                        inventory->adjustCount(door.requiredItem, -1);
                    }
                    door.doorOpen = !door.doorOpen;
                    break;
                }
                case PropPrototypeId.PlaceableLamp:
                    prop->data.lamp.on = !prop->data.lamp.on;
                    break;
                default:
                    return;
            }

            world.data.dirty = true;
        }
    }
}
