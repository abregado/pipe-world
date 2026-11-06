using UnityEngine;

namespace PipeWorld.Piper
{
    // Runs when the game starts with -piperSelfTest: exercises every InputAction and a save/reset/load round trip, logs the result and quits.
    public unsafe class PiperSelfTest
    {
        private readonly PiperGame game;
        private int step;
        private int waitTicks;
        private int propCountBefore;

        private static readonly Vector3 LampPosition = new Vector3(12f, 0.05f, 0f);
        private static readonly Vector3 SalvagePosition = new Vector3(12f, 0.05f, 2f);
        private static readonly Vector3 SalvageMoved = new Vector3(12f, 1.05f, 2f);

        public PiperSelfTest(PiperGame game)
        {
            this.game = game;
        }

        public void tick()
        {
            if (waitTicks-- > 0)
                return;

            World world = game.world;
            InputActionManager actions = game.inputActionManager;

            switch (step)
            {
                case 0:
                    if (CountProps(world) < 8)
                        return;
                    propCountBefore = CountProps(world);
                    actions.add(new AdjustInventoryData { item = InventoryItemPrototypeId.Obtainium, amount = 10 });
                    actions.add(new AdjustInventoryData { item = InventoryItemPrototypeId.LightUnit, amount = 1 });
                    actions.add(new PlacePropUsingItemData { propPrototypeId = PropPrototypeId.PlaceableLamp, position = LampPosition, eulerRotation = Vec3f.INVALID });
                    actions.add(new PlacePropUsingItemData { propPrototypeId = PropPrototypeId.PlaceableLamp, position = LampPosition + Vector3.forward * 5f, eulerRotation = Vec3f.INVALID });
                    actions.add(new PlacePropData { propPrototypeId = PropPrototypeId.CollectableGeneric, position = SalvagePosition });
                    Next(5);
                    break;

                case 1:
                    Check(world.data.inventory.getCount(InventoryItemPrototypeId.Obtainium) == 10, "obtainium granted");
                    Check(world.data.inventory.getCount(InventoryItemPrototypeId.LightUnit) == 0, "light unit spent by the lamp placement, second placement refused");
                    Check(CountProps(world) == propCountBefore + 2, "lamp and salvage placed");
                    actions.add(new MovePropData { prop = Id(PropPrototypeId.CollectableGeneric, SalvagePosition), newPosition = SalvageMoved });
                    actions.add(new DeletePropData { prop = Id(PropPrototypeId.PlaceableLamp, LampPosition) });
                    Next(5);
                    break;

                case 2:
                    Check(CountProps(world) == propCountBefore + 1, "lamp deleted");
                    Check(Id(PropPrototypeId.CollectableGeneric, SalvageMoved).tryGet(world) != null, "salvage moved");
                    actions.add(new CollectCollectableData { prop = Id(PropPrototypeId.CollectableGeneric, SalvageMoved) });
                    Next(5);
                    break;

                case 3:
                    Check(CountProps(world) == propCountBefore, "salvage collected");
                    Check(world.data.inventory.getCount(InventoryItemPrototypeId.Obtainium) == 10, "inventory unchanged by salvage of another item");
                    string path = game.SaveGameNow();
                    int obtainium = world.data.inventory.getCount(InventoryItemPrototypeId.Obtainium);
                    int props = CountProps(world);
                    game.ReloadFromDiskForTest(path);
                    Check(game.world.data.inventory.getCount(InventoryItemPrototypeId.Obtainium) == obtainium, "inventory survives save/load");
                    Check(CountProps(game.world) == props, "props survive save/load");
                    Finish();
                    break;
            }
        }

        private void Next(int wait)
        {
            step++;
            waitTicks = wait;
        }

        private static PropNetworkIdentifier Id(PropPrototypeId id, Vector3 position)
        {
            return new PropNetworkIdentifier { propPrototypeId = id, position = position };
        }

        private static int CountProps(World world)
        {
            int count = 0;
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                if (world.props.slabs.get(i)->slabHeader.allocated)
                    count++;
            }
            return count;
        }

        private bool failed;

        private void Check(bool ok, string what)
        {
            if (!ok)
                failed = true;
            Debug.Log($"[Piper] selftest {(ok ? "ok  " : "FAIL")} {what}");
        }

        private void Finish()
        {
            step = -1;
            Debug.Log(failed ? "[Piper] selftest FAILED" : "[Piper] selftest PASSED");
            Application.Quit();
        }
    }
}
