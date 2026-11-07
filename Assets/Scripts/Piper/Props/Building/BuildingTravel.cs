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
            Vector3 target = m.position + rotation * (position + step * StepDistance);
            if (inside)
                target.y = GroundHeight(target, m.groundY);
            else
                target.y = m.position.y;

            Vector3 facing = rotation * step;
            game.playerObject.Teleport(target + Vector3.up * 0.05f, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 0f);
        }

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
