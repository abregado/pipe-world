using UnityEngine;

namespace PipeWorld.Piper
{
    // The box that encloses a prop's meshes, measured in the prop's own rotation so a rotated prop gets a rotated box.
    public static class PropBoundsHelper
    {
        // Centre and size in the prop root's local space (before scale).
        public static bool Local(PropBehaviour prop, out Vector3 centre, out Vector3 size)
        {
            Transform root = prop.transform;
            bool any = false;
            Bounds bounds = default;
            foreach (MeshFilter filter in prop.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        any = true;
                    }
                    else
                        bounds.Encapsulate(local);
                }
            }

            if (!any)
            {
                centre = Vector3.zero;
                size = Vector3.one * 0.5f;
                return false;
            }
            centre = bounds.center;
            size = bounds.size;
            return true;
        }

        // Centre, X axis and size in world space for BuildingGizmos.Box.
        public static void World(PropBehaviour prop, out Vector3 centre, out Vector3 along, out Vector3 size)
        {
            Local(prop, out Vector3 localCentre, out Vector3 localSize);
            Transform root = prop.transform;
            centre = root.TransformPoint(localCentre);
            along = root.right;
            size = Vector3.Scale(localSize, root.lossyScale);
        }
    }
}
