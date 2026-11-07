using System.Linq;
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
        private uint buildingId;
        private uint originalArea;
        private string buildingCounts;

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
                    if (CountProps(world) < 8 || CountBuildings(world) < 1)
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
                {
                    Check(world.buildings.slabs.size > 0 && CountBuildings(world) == 1, "seeded building exists");
                    buildingId = FirstBuilding(world);
                    BuildingModel m = BuildingStore.load(world, buildingId);
                    Check(m.vertices.Count == 4 && m.edges.Count == 4 && m.areas.Count == 1 && m.objects.Count == 2 && m.height == 3f && m.groundY <= m.position.y,
                        "new building is a 3x3 room with an exterior door and a terminal");
                    originalArea = m.areas[0].id;
                    Check(FuzzSlices(m), "slicing at a grid of points never throws");
                    float pull = BuildingTargeting.ClosestOnLine(new Ray(new Vector3(3f, 5f, -5f), new Vector3(0f, -1f, 1f).normalized), Vector3.zero, Vector3.right);
                    Check(Mathf.Abs(pull - 3f) < 0.01f, "crosshair pull along a constrained axis");
                    BObject door = m.objects.Find(o => o.proto == PropPrototypeId.ExteriorDoor);
                    Check(m.roomOnSide(m.edge(door.faceRef), door.face) == null, "exterior door faces outwards");
                    actions.add(new ExtrudeBuildingEdgeData { building = buildingId, edge = door.faceRef, offset = 2f });
                    actions.add(new ExtrudeBuildingEdgeData { building = buildingId, edge = door.faceRef, offset = -1f });
                    Next(5);
                    break;
                }

                case 4:
                {
                    BuildingModel m = BuildingStore.load(world, buildingId);
                    Check(m.areas.Count == 2 && m.vertices.Count == 6 && m.edges.Count == 7, "extrude added a room; the inward extrude was rejected");
                    BObject door = m.objects.Find(o => o.proto == PropPrototypeId.ExteriorDoor);
                    Check(m.isEdgeOutside(door.faceRef) && m.roomOnSide(m.edge(door.faceRef), door.face) == null, "exterior door moved to an outer wall and faces outwards");
                    Check(m.validate() == null, "building valid after extrude");

                    BArea added = m.areas.Find(a => a.id != originalArea);
                    var c = m.areaCentroid(added);
                    actions.add(new SliceBuildingAreaData { building = buildingId, area = added.id, x = -1.4f, z = (float)c.z });

                    // West wall of the original room (the door moved to the east wall): its positive side faces into the room.
                    BEdge east = m.edges.Find(e => m.vertex(e.a).x == -1.5f && m.vertex(e.b).x == -1.5f && m.vertex(e.a).z != m.vertex(e.b).z);
                    actions.add(new PlaceBuildingPropData { building = buildingId, proto = PropPrototypeId.Table, face = BuildingFace.Floor, faceRef = originalArea, u = 0.3f, v = 0.3f });
                    actions.add(new PlaceBuildingPropData { building = buildingId, proto = PropPrototypeId.Chair, face = BuildingFace.Floor, faceRef = originalArea, u = 0.3f, v = 0.3f });
                    actions.add(new PlaceBuildingPropData { building = buildingId, proto = PropPrototypeId.CeilingLamp, face = BuildingFace.Floor, faceRef = originalArea, u = 0.7f, v = 0.3f });
                    actions.add(new PlaceBuildingPropData { building = buildingId, proto = PropPrototypeId.CeilingLamp, face = BuildingFace.Ceiling, faceRef = originalArea, u = 0.7f, v = 0.3f });
                    actions.add(new PlaceBuildingPropData { building = buildingId, proto = PropPrototypeId.Painting, face = BuildingFace.WallPositive, faceRef = east.id, u = 0.5f, v = 0.55f });
                    actions.add(new PlaceBuildingPropData { building = buildingId, proto = PropPrototypeId.Archway, face = BuildingFace.WallPositive, faceRef = east.id, u = 0.5f, v = 0f });
                    Next(5);
                    break;
                }

                case 5:
                {
                    BuildingModel m = BuildingStore.load(world, buildingId);
                    Check(m.areas.Count == 3, "slice split a room in two");
                    Check(m.objects.Count(o => o.proto == PropPrototypeId.Table) == 1 && m.objects.Count(o => o.proto == PropPrototypeId.Chair) == 0, "table placed; a chair on top of it refused");
                    Check(m.objects.Count(o => o.proto == PropPrototypeId.CeilingLamp) == 1 && m.objects.Find(o => o.proto == PropPrototypeId.CeilingLamp).face == BuildingFace.Ceiling, "lamp placed on a ceiling, refused on a floor");
                    Check(m.objects.Any(o => o.proto == PropPrototypeId.Painting), "painting hung on an outer wall facing the room");
                    Check(!m.objects.Any(o => o.proto == PropPrototypeId.Archway), "archway refused on an outer wall");
                    Check(m.validate() == null, "building valid after slice");

                    BEdge north = m.edges.Find(e => m.vertex(e.a).z == 1.5f && m.vertex(e.b).z == 1.5f);
                    actions.add(new MoveBuildingEdgeData { building = buildingId, edge = north.id, offset = 0.5f });
                    actions.add(new DeleteBuildingObjectData { building = buildingId, obj = m.objects.Find(o => o.proto == PropPrototypeId.ExteriorDoor).id });
                    actions.add(new SetBuildingHeightsData { building = buildingId, floorY = m.position.y + 1f, height = 1f });
                    actions.add(new SetBuildingHeightsData { building = buildingId, floorY = m.position.y + 1f, height = 3.5f });
                    Next(5);
                    break;
                }

                case 6:
                {
                    BuildingModel m = BuildingStore.load(world, buildingId);
                    Check(m.validate() == null, "building valid after moving a wall");
                    Check(m.objects.Any(o => o.proto == PropPrototypeId.ExteriorDoor), "the last exterior door cannot be deleted");
                    Check(m.height == 3.5f && Mathf.Abs(m.position.y - m.groundY - 1f) < 0.001f, "heights set; a ceiling of 1 m refused");

                    BObject painting = m.objects.Find(o => o.proto == PropPrototypeId.Painting);
                    Vector3 paintingLocal = m.objectLocalPosition(painting);
                    Check(Mathf.Abs(paintingLocal.z + 0.25f) < 0.01f && Mathf.Abs(paintingLocal.y - 0.55f * 3.5f) < 0.01f, "painting followed its wall when the wall shrank and the ceiling rose");
                    Check(PropWorldMatches(world, m, painting), "prop world position follows the building");
                    Check(PropWorldMatches(world, m, m.objects.Find(o => o.proto == PropPrototypeId.Terminal)), "terminal rose with the floor");
                    buildingCounts = CountEntities(world);
                    Next(1);
                    break;
                }

                case 7:
                    Check(CountProps(world) == propCountBefore, "salvage collected");
                    Check(world.data.inventory.getCount(InventoryItemPrototypeId.Obtainium) == 10, "inventory unchanged by salvage of another item");
                    string path = game.SaveGameNow();
                    int obtainium = world.data.inventory.getCount(InventoryItemPrototypeId.Obtainium);
                    int props = CountProps(world);
                    game.ReloadFromDiskForTest(path);
                    Check(game.world.data.inventory.getCount(InventoryItemPrototypeId.Obtainium) == obtainium, "inventory survives save/load");
                    Check(CountProps(game.world) == props, "props survive save/load");
                    Check(CountEntities(game.world) == buildingCounts, "building data survives save/load");
                    Check(BuildingStore.load(game.world, buildingId).validate() == null, "building valid after load");
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

        private bool FuzzSlices(BuildingModel m)
        {
            int next = 1;
            m.newId = () => (uint)(0x700000 + next++);
            bool ok = true;
            for (float x = -1.4f; x <= 1.41f; x += 0.35f)
            {
                for (float z = -1.4f; z <= 1.41f; z += 0.35f)
                {
                    try
                    {
                        BuildingModel work = m.Clone();
                        string error = work.slice(m.areas[0].id, x, z);
                        if (error == null && work.areas.Any(a => a.edges.Count < 3))
                        {
                            Debug.Log($"[Piper] slice at {x},{z} produced a degenerate room");
                            ok = false;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log($"[Piper] slice at {x},{z} threw {e.GetType().Name}");
                        ok = false;
                    }
                }
            }
            return ok;
        }

        private static bool PropWorldMatches(World world, BuildingModel m, BObject o)
        {
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* p = world.props.slabs.get(i);
                if (!p->slabHeader.allocated || p->slabHeader._playerIndexAndGeneration != o.id)
                    continue;
                Vector3 expected = m.position + Quaternion.Euler(0f, m.yaw, 0f) * m.objectLocalPosition(o);
                return Vector3.Distance(expected, p->position) < 0.001f && p->building == m.id;
            }
            return false;
        }

        private static int CountBuildings(World world)
        {
            int count = 0;
            for (int i = 0; i < world.buildings.slabs.size; i++)
                if (world.buildings.slabs.get(i)->slabHeader.allocated)
                    count++;
            return count;
        }

        private static uint FirstBuilding(World world)
        {
            for (int i = 0; i < world.buildings.slabs.size; i++)
                if (world.buildings.slabs.get(i)->slabHeader.allocated)
                    return world.buildings.slabs.get(i)->slabHeader._playerIndexAndGeneration;
            return 0;
        }

        private static string CountEntities(World world)
        {
            BuildingModel m = BuildingStore.load(world, FirstBuilding(world));
            return $"{m.vertices.Count}/{m.edges.Count}/{m.areas.Count}/{m.objects.Count}/{m.position}/{m.height}/{string.Join(",", m.vertices.Select(v => $"{v.id}:{v.x}:{v.z}"))}/{string.Join(",", m.objects.Select(o => $"{o.id}:{o.u}:{o.v}"))}";
        }

        // Props that belong to a building are counted with that building, not here.
        private static int CountProps(World world)
        {
            int count = 0;
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* p = world.props.slabs.get(i);
                if (p->slabHeader.allocated && p->building == 0)
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
