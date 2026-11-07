using UnityEngine;

namespace PipeWorld.Piper
{
    // Turning the crosshair ray into building-space targets.
    public static class BuildingTargeting
    {
        public static Ray ViewRay(PiperGame game)
        {
            Transform view = game.raycaster.viewTransform;
            return new Ray(view.position, view.forward);
        }

        public static Matrix4x4 ToWorld(BuildingModel m) => Matrix4x4.TRS(m.position, Quaternion.Euler(0f, m.yaw, 0f), Vector3.one);

        public static bool RayPlane(Ray ray, float y, out Vector3 point)
        {
            point = default;
            if (Mathf.Abs(ray.direction.y) < 1e-4f)
                return false;
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t <= 0f)
                return false;
            point = ray.GetPoint(t);
            return true;
        }

        // Offset along the line through p0 with direction n that is closest to the ray: the crosshair's pull along a constrained axis.
        public static float ClosestOnLine(Ray ray, Vector3 p0, Vector3 n)
        {
            Vector3 w0 = p0 - ray.origin;
            float b = Vector3.Dot(n, ray.direction);
            float d = Vector3.Dot(n, w0);
            float e = Vector3.Dot(ray.direction, w0);
            float denom = 1f - b * b;
            if (denom < 1e-4f)
                return 0f;
            return (b * e - d) / denom;
        }

        public static float DistanceToRay(Ray ray, Vector3 point, out float along)
        {
            Vector3 to = point - ray.origin;
            along = Vector3.Dot(to, ray.direction);
            return Vector3.Cross(ray.direction, to).magnitude;
        }

        public static BuildingView ViewOf(Collider collider) => collider.GetComponentInParent<BuildingView>();

        public static float Round(float v) => Mathf.Round(v * 100f) / 100f;
    }
}
