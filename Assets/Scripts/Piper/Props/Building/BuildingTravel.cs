using UnityEngine;

namespace PipeWorld.Piper
{
    // Moving the player through an exterior door. Walls stay solid; the door only relocates the player to the other side.
    public static class BuildingTravel
    {
        private const float StepDistance = 1.5f;

        public static bool IsInside(BuildingModel m, Vector3 worldPosition)
        {
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, m.yaw, 0f)) * (worldPosition - m.position);
            return m.areaAt(local.x, local.z) != null;
        }

        public static bool IsLocalPlayerInside(PiperGame game, uint buildingId)
        {
            BuildingModel m = BuildingStore.load(game.world, buildingId);
            return m != null && IsInside(m, game.playerObject.transform.position);
        }

        // Whether the local player stands inside the footprint of any building (used to decide which props they can place).
        // Whether a point is on the roof of the building: within its footprint but above the ceiling.
        public static bool IsOnRoof(BuildingModel m, Vector3 worldPosition) =>
            IsInside(m, worldPosition) && worldPosition.y > m.position.y + m.height - 0.5f;

        // The ladder joins a room to the roof above it. From inside it puts the player on the roof beside the hatch, and from the roof
        // it puts them on the floor beside the ladder, both a step towards the middle of the room.
        public static void UseLadder(PiperGame game, uint buildingId, uint objectId)
        {
            BuildingModel m = BuildingStore.load(game.world, buildingId);
            BObject ladder = m?.objects.Find(o => o.id == objectId);
            if (ladder == null || ladder.def.surface != PropSurface.Ceiling)
                return;

            Vector3 local = m.objectLocalPosition(ladder);
            var centre = m.areaCentroid(m.area(ladder.faceRef));
            Vector3 toward = new Vector3((float)centre.x - local.x, 0f, (float)centre.z - local.z);
            toward = toward.sqrMagnitude < 0.01f ? Vector3.right : toward.normalized;

            Quaternion rotation = Quaternion.Euler(0f, m.yaw, 0f);
            Vector3 target = m.position + rotation * (new Vector3(local.x, 0f, local.z) + toward * 0.9f);
            bool onRoof = IsOnRoof(m, game.playerObject.transform.position);
            target.y = onRoof ? m.position.y : m.position.y + m.height + BuildingConstants.RoofThickness;

            Vector3 facing = rotation * toward;
            game.playerObject.Teleport(target + Vector3.up * 0.05f, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 0f);
        }

        public static unsafe bool IsLocalPlayerInsideAny(PiperGame game)
        {
            Vector3 p = game.playerObject.transform.position;
            for (int i = 0; i < game.world.buildings.slabs.size; i++)
            {
                Building* b = game.world.buildings.slabs.get(i);
                if (!b->slabHeader.allocated || Vector3.Distance(b->position, p) > 60f)
                    continue;
                BuildingModel m = BuildingStore.load(game.world, b->slabHeader._playerIndexAndGeneration);
                if (m != null && IsInside(m, p))
                    return true;
            }
            return false;
        }

        public static void UseDoor(PiperGame game, uint buildingId, uint objectId)
        {
            BuildingModel m = BuildingStore.load(game.world, buildingId);
            BObject door = m?.objects.Find(o => o.id == objectId);
            if (door == null || door.def.surface != PropSurface.Wall)
                return;

            // The door faces outwards; the room is behind it.
            Vector3 outward = m.sideNormal(m.edge(door.faceRef), door.face);
            Vector3 position = m.objectLocalPosition(door);
            position.y = 0f;
            Quaternion rotation = Quaternion.Euler(0f, m.yaw, 0f);
            bool inside = IsInside(m, game.playerObject.transform.position);

            Vector3 step = inside ? outward : -outward;
            Vector3 facing = rotation * step;
            Vector3 doorWorld = m.position + rotation * position;
            Vector3 target = FindClearSpot(doorWorld, facing, inside, m);
            game.playerObject.Teleport(target + Vector3.up * 0.05f, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 0f);
        }

        // Where the player is put through a door: the nearest spot to the door where a box the size of a standing player touches nothing.
        // It tries a little further out each time, and a little to each side, so furniture, stairs or rocks in front of the door never
        // trap the player. Outside the spot is on the ground; inside it is on the floor.
        private static Vector3 FindClearSpot(Vector3 doorWorld, Vector3 forward, bool outside, BuildingModel m)
        {
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 half = new Vector3(0.35f, 0.8f, 0.35f);
            Quaternion orientation = Quaternion.LookRotation(forward);
            Vector3 fallback = Vector3.zero;
            bool haveFallback = false;

            foreach (float distance in ClearDistances)
            {
                foreach (float lateral in ClearOffsets)
                {
                    Vector3 spot = doorWorld + forward * distance + side * lateral;
                    spot.y = outside ? GroundHeight(spot, m.groundY) : m.position.y;
                    if (!haveFallback)
                    {
                        fallback = spot;
                        haveFallback = true;
                    }

                    // The box starts a little above the ground so the ground itself is not a hit.
                    Vector3 centre = spot + Vector3.up * (0.15f + half.y);
                    if (!Physics.CheckBox(centre, half, orientation, ~0, QueryTriggerInteraction.Ignore))
                        return spot;
                }
            }
            return fallback;
        }

        private static readonly float[] ClearDistances = { 1.3f, 1.6f, 2f, 2.5f, 3f, 3.5f, 4f };
        private static readonly float[] ClearOffsets = { 0f, 0.5f, -0.5f, 1f, -1f };

        // Ground under a point outside, ignoring buildings so a roof never counts as ground.
        public static float GroundHeight(Vector3 near, float fallback)
        {
            RaycastHit[] hits = Physics.RaycastAll(near + Vector3.up * 60f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<BuildingView>() != null || hit.collider.GetComponentInParent<PropBehaviour>() != null)
                    continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = hit.point.y;
                }
            }
            return float.IsNegativeInfinity(best) ? fallback : best;
        }
    }
}
