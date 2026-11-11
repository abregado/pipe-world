using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Inventories live in buildings, but only a building with a Cargo Terminal inside is reachable. Items collected and spent come from the
    // nearest reachable building, and the HUD shows the inventory of the one the player is in range of. A building that loses its last
    // Cargo Terminal keeps what it stored; it just stops being found. Every peer picks the same building because ties go to the lower
    // entity id, not the slab slot. Distances are measured on the ground plane, to match the radius drawn on the ground.
    public static unsafe class BuildingInventory
    {
        // How close the player must be to a building for its inventory to show.
        public const float Range = 30f;

        private static readonly HashSet<uint> cargoBuildings = new HashSet<uint>();

        // The buildings that have at least one Cargo Terminal. The set is reused, so it is only valid until the next call.
        public static HashSet<uint> buildingsWithCargo(World world)
        {
            cargoBuildings.Clear();
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* p = world.props.slabs.get(i);
                if (p->slabHeader.allocated && p->building != 0 && p->prototypeId == PropPrototypeId.CargoTerminal)
                    cargoBuildings.Add(p->building);
            }
            return cargoBuildings;
        }

        public static float groundDistance(Building* b, Vector3 point) =>
            Vector2.Distance(new Vector2(b->position.x, b->position.z), new Vector2(point.x, point.z));

        public static Building* nearest(World world, Vector3 point, float maxDistance = float.MaxValue)
        {
            HashSet<uint> reachable = buildingsWithCargo(world);
            Building* best = null;
            float bestDistance = maxDistance;
            uint bestId = 0;

            for (int i = 0; i < world.buildings.slabs.size; i++)
            {
                Building* b = world.buildings.slabs.get(i);
                if (!b->slabHeader.allocated)
                    continue;

                uint id = b->slabHeader._playerIndexAndGeneration;
                if (!reachable.Contains(id))
                    continue;

                float d = groundDistance(b, point);
                if (d < bestDistance || (best != null && d == bestDistance && id < bestId))
                {
                    best = b;
                    bestDistance = d;
                    bestId = id;
                }
            }
            return best;
        }

        // The inventory items near a point are taken from and given to: that of the nearest reachable building within Range, or null
        // when none is in range.
        public static Inventory* at(World world, Vector3 point)
        {
            Building* b = nearest(world, point, Range);
            return b == null ? null : &b->inventory;
        }

        public const string OutOfRangeMessage = "No base inventory in range.";

        public static bool inRange(World world, Vector3 point) => nearest(world, point, Range) != null;
    }
}
