using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Draws the reach of building inventories as a line on the ground: while a building is being edited (every reachable building, plus
    // the edited one) and whenever the player comes within a few units of a building's radius.
    public unsafe class InventoryRadiusDisplay
    {
        private const int Samples = 96;
        private const float ShowWithin = 4f;
        private const float HeightCacheSeconds = 3f;
        private const float Lift = 0.2f;

        private static readonly Color Reachable = new Color(1f, 0.85f, 0.3f);
        private static readonly Color Unreachable = new Color(0.6f, 0.6f, 0.62f);

        private class Ring
        {
            public Vector3[] points = new Vector3[Samples];
            public float stamp = float.NegativeInfinity;
        }

        private readonly BuildingGizmos gizmos = new BuildingGizmos("Inventory radius");
        private readonly Dictionary<uint, Ring> rings = new Dictionary<uint, Ring>();

        public void Clear() => gizmos.Clear();

        public void update(World world, Vector3 playerPosition, uint editingBuilding, bool visible)
        {
            gizmos.Begin();
            if (visible)
            {
                HashSet<uint> reachable = BuildingInventory.buildingsWithCargo(world);
                for (int i = 0; i < world.buildings.slabs.size; i++)
                {
                    Building* b = world.buildings.slabs.get(i);
                    if (!b->slabHeader.allocated)
                        continue;

                    uint id = b->slabHeader._playerIndexAndGeneration;
                    bool hasCargo = reachable.Contains(id);
                    bool editing = editingBuilding != 0 && (hasCargo || id == editingBuilding);
                    bool near = hasCargo && Mathf.Abs(BuildingInventory.groundDistance(b, playerPosition) - BuildingInventory.Range) < ShowWithin;
                    if (editing || near)
                        Draw(b, id, hasCargo ? Reachable : Unreachable);
                }
            }
            gizmos.End();
        }

        private void Draw(Building* b, uint id, Color color)
        {
            if (!rings.TryGetValue(id, out Ring ring))
            {
                ring = new Ring();
                rings[id] = ring;
            }

            // Ground heights are measured now and then, not every frame.
            if (Time.realtimeSinceStartup - ring.stamp > HeightCacheSeconds)
            {
                ring.stamp = Time.realtimeSinceStartup;
                Vector3 centre = b->position;
                for (int i = 0; i < Samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / Samples;
                    Vector3 p = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * BuildingInventory.Range;
                    p.y = BuildingTravel.GroundHeight(p, b->groundY) + Lift;
                    ring.points[i] = p;
                }
            }

            for (int i = 0; i < Samples; i++)
                gizmos.Line(ring.points[i], ring.points[(i + 1) % Samples], color, 0.1f);
        }
    }
}
