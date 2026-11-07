using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PipeWorld.Piper
{
    public enum PropSurface : byte { Floor, Ceiling, Wall }

    // The placement rules of one prop that can be attached to a building.
    public class BuildingPropDef
    {
        public PropPrototypeId id;
        public string name;
        public PropSurface surface;
        // Floor and ceiling props: the footprint radius they need clear.
        public float radius;
        // Floor and ceiling props: the height of the volume shown while placing it.
        public float previewHeight;
        // Wall props: the size of the rectangle they cover. Bottom-anchored props (doors) start at the floor, others are centred on their height.
        public float width, height;
        public bool anchorBottom;
        public bool partitionOnly, outerOnly;
        // Cuts a walkable opening through the wall.
        public bool opening;
        // Moves the player to the other side of the wall when used.
        public bool teleports;
        // Reaches both sides of the wall, so nothing else may share its rectangle on either side.
        public bool bothSides;
        // The building cannot lose its last one.
        public bool essential;
        public bool placeable;
    }

    public static class BuildingProps
    {
        public static readonly BuildingPropDef[] all =
        {
            new BuildingPropDef { id = PropPrototypeId.Table, name = "Table", surface = PropSurface.Floor, radius = 0.5f, previewHeight = 0.75f, placeable = true },
            new BuildingPropDef { id = PropPrototypeId.Chair, name = "Chair", surface = PropSurface.Floor, radius = 0.28f, previewHeight = 0.9f, placeable = true },
            new BuildingPropDef { id = PropPrototypeId.CeilingLamp, name = "Ceiling lamp", surface = PropSurface.Ceiling, radius = 0.3f, previewHeight = 0.35f, placeable = true },
            new BuildingPropDef { id = PropPrototypeId.Painting, name = "Painting", surface = PropSurface.Wall, width = 0.9f, height = 0.6f, placeable = true },
            new BuildingPropDef { id = PropPrototypeId.BuildingDoor, name = "Door", surface = PropSurface.Wall, width = 0.9f, height = 2.1f, anchorBottom = true, partitionOnly = true, opening = true, bothSides = true, placeable = true },
            new BuildingPropDef { id = PropPrototypeId.Archway, name = "Archway", surface = PropSurface.Wall, width = 1.3f, height = 2.3f, anchorBottom = true, partitionOnly = true, opening = true, bothSides = true, placeable = true },
            new BuildingPropDef { id = PropPrototypeId.ExteriorDoor, name = "Exterior door", surface = PropSurface.Wall, width = 1.1f, height = 2.1f, anchorBottom = true, outerOnly = true, teleports = true, bothSides = true, essential = true, placeable = true },
            new BuildingPropDef { id = PropPrototypeId.Terminal, name = "Terminal", surface = PropSurface.Floor, radius = 0.4f, previewHeight = 1.2f, essential = true },
        };

        public static readonly BuildingPropDef[] placeable = all.Where(d => d.placeable).ToArray();

        public static BuildingPropDef get(PropPrototypeId id)
        {
            foreach (BuildingPropDef d in all)
                if (d.id == id)
                    return d;
            return null;
        }

        // The vertical position a wall prop gets when it is placed at a given height above the floor.
        public static float snapHeight(BuildingPropDef def, float yAboveFloor, float ceilingHeight)
        {
            if (def.anchorBottom)
                return 0f;
            float y = Mathf.Round(yAboveFloor * 10f) / 10f;
            return Mathf.Clamp(y, def.height * 0.5f + 0.2f, ceilingHeight - def.height * 0.5f - 0.2f) / ceilingHeight;
        }
    }

    // A prop attached to a face of a building. u and v run 0 to 1 across the face, so the prop keeps its place when the face is resized:
    // floors and ceilings use the area's bounding box in building space, walls use the distance along the edge and the fraction of the
    // ceiling height.
    public class BObject
    {
        public uint id;
        public PropPrototypeId proto;
        public BuildingFace face;
        public uint faceRef;
        public float u, v;

        public BuildingPropDef def => BuildingProps.get(proto);
    }

    public partial class BuildingModel
    {
        // ---- Faces ----

        public (double minX, double maxX, double minZ, double maxZ) areaBounds(BArea a)
        {
            var verts = areaVertices(a);
            return (verts.Min(p => (double)p.x), verts.Max(p => (double)p.x), verts.Min(p => (double)p.z), verts.Max(p => (double)p.z));
        }

        public (double x, double z) areaPoint(BArea a, float u, float v)
        {
            var b = areaBounds(a);
            return (b.minX + (b.maxX - b.minX) * u, b.minZ + (b.maxZ - b.minZ) * v);
        }

        public (float u, float v) areaUv(BArea a, double x, double z)
        {
            var b = areaBounds(a);
            double w = b.maxX - b.minX, h = b.maxZ - b.minZ;
            return (w < 1e-6 ? 0.5f : (float)Math.Clamp((x - b.minX) / w, 0, 1), h < 1e-6 ? 0.5f : (float)Math.Clamp((z - b.minZ) / h, 0, 1));
        }

        // The room on one side of a wall, or null if that side is outdoors.
        public BArea roomOnSide(BEdge e, BuildingFace side)
        {
            var n = edgeNormal(e);
            var m = edgeMidpoint(e);
            foreach (BArea a in areasForEdge(e.id))
            {
                var c = areaCentroid(a);
                bool positive = n.x * (c.x - m.x) + n.z * (c.z - m.z) > 0;
                if (positive == (side == BuildingFace.WallPositive))
                    return a;
            }
            return null;
        }

        // The unit direction pointing out of a wall's face.
        public Vector3 sideNormal(BEdge e, BuildingFace side)
        {
            var n = edgeNormal(e);
            Vector3 v = new Vector3((float)n.x, 0f, (float)n.z);
            return side == BuildingFace.WallPositive ? v : -v;
        }

        public BuildingFace outerSide(BEdge e) => roomOnSide(e, BuildingFace.WallPositive) == null ? BuildingFace.WallPositive : BuildingFace.WallNegative;

        // The side of a wall that a point is on.
        public BuildingFace sideOf(BEdge e, double x, double z)
        {
            var n = edgeNormal(e);
            var m = edgeMidpoint(e);
            return n.x * (x - m.x) + n.z * (z - m.z) >= 0 ? BuildingFace.WallPositive : BuildingFace.WallNegative;
        }

        // ---- Transforms ----

        public Vector3 objectLocalPosition(BObject o)
        {
            BuildingPropDef def = o.def;
            if (def.surface == PropSurface.Wall)
            {
                BEdge e = edge(o.faceRef);
                BVertex a = vertex(e.a), b = vertex(e.b);
                return new Vector3(a.x + (b.x - a.x) * o.u, o.v * height, a.z + (b.z - a.z) * o.u);
            }

            var p = areaPoint(area(o.faceRef), o.u, o.v);
            return new Vector3((float)p.x, def.surface == PropSurface.Ceiling ? height : 0f, (float)p.z);
        }

        // The yaw about Y that points the prop's +Z out of its face.
        public float objectLocalYaw(BObject o)
        {
            if (o.def.surface != PropSurface.Wall)
                return 0f;
            Vector3 f = sideNormal(edge(o.faceRef), o.face);
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        public (double s0, double s1, double y0, double y1) wallRect(BObject o)
        {
            BuildingPropDef def = o.def;
            double s = edgeLength(edge(o.faceRef)) * o.u;
            double y = o.v * height;
            double y0 = def.anchorBottom ? y : y - def.height * 0.5;
            return (s - def.width * 0.5, s + def.width * 0.5, y0, y0 + def.height);
        }

        // Moves u so a wall prop sits clear of the ends of its wall.
        public float clampWallU(BuildingPropDef def, BEdge e, float u)
        {
            double len = edgeLength(e);
            double min = (def.width * 0.5 + BuildingConstants.PropMargin) / len;
            if (min >= 0.5) return 0.5f;
            return (float)Math.Clamp(u, min, 1 - min);
        }

        // ---- Rules ----

        private bool conflicts(BObject o, BObject other)
        {
            BuildingPropDef def = o.def, od = other.def;
            if (def.surface != od.surface)
                return false;

            if (def.surface == PropSurface.Wall)
            {
                if (other.faceRef != o.faceRef)
                    return false;
                if (o.face != other.face && !def.bothSides && !od.bothSides)
                    return false;
                var a = wallRect(o);
                var b = wallRect(other);
                return a.s0 < b.s1 && b.s0 < a.s1 && a.y0 < b.y1 && b.y0 < a.y1;
            }

            BArea areaA = area(o.faceRef), areaB = area(other.faceRef);
            if (areaA == null || areaB == null)
                return false;
            var pa = areaPoint(areaA, o.u, o.v);
            var pb = areaPoint(areaB, other.u, other.v);
            return BuildingGeometry.dist(pa.x, pa.z, pb.x, pb.z) < def.radius + od.radius;
        }

        // Null when the prop is valid on its face. `others` are the props it must not overlap; null skips that check.
        public string propProblem(BObject o, IEnumerable<BObject> others)
        {
            BuildingPropDef def = o.def;
            if (def == null)
                return "Unknown prop.";

            if (def.surface == PropSurface.Wall)
            {
                if (o.face != BuildingFace.WallPositive && o.face != BuildingFace.WallNegative)
                    return def.name + " goes on a wall.";
                BEdge e = edge(o.faceRef);
                if (e == null)
                    return "There is no wall here.";

                bool partition = isEdgeInside(e.id), outer = isEdgeOutside(e.id);
                if (def.partitionOnly && !partition)
                    return def.name + " needs a wall between two rooms.";
                if (def.outerOnly)
                {
                    if (!outer) return def.name + " needs an outer wall.";
                    if (roomOnSide(e, o.face) != null) return def.name + " opens onto the outside.";
                }
                else if (roomOnSide(e, o.face) == null)
                    return def.name + " goes on a wall that faces a room.";

                double len = edgeLength(e);
                var r = wallRect(o);
                if (len < def.width + BuildingConstants.PropMargin * 2) return "The wall is too short.";
                if (r.s0 < BuildingConstants.PropMargin - 1e-4 || r.s1 > len - BuildingConstants.PropMargin + 1e-4) return "Too close to the end of the wall.";
                if (r.y0 < -1e-4 || r.y1 > height + 1e-4) return def.name + " does not fit the wall height.";
            }
            else
            {
                BuildingFace wanted = def.surface == PropSurface.Floor ? BuildingFace.Floor : BuildingFace.Ceiling;
                if (o.face != wanted)
                    return def.name + (def.surface == PropSurface.Floor ? " goes on a floor." : " goes on a ceiling.");

                BArea a = area(o.faceRef);
                if (a == null)
                    return "Place it inside a room.";
                var p = areaPoint(a, o.u, o.v);
                var poly = areaPolygon(a);
                if (!BuildingGeometry.pointInPolygon(p.x, p.z, poly))
                    return "Place it inside a room.";
                for (int i = 0; i < poly.Count; i++)
                {
                    int j = (i + 1) % poly.Count;
                    if (BuildingGeometry.distPointToSegment(p.x, p.z, poly[i].x, poly[i].z, poly[j].x, poly[j].z) < def.radius)
                        return "Too close to a wall.";
                }
            }

            if (others != null)
            {
                foreach (BObject other in others)
                    if (other.id != o.id && other.def != null && conflicts(o, other))
                        return "Something is already there.";
            }
            return null;
        }

        // ---- Edits ----

        // The prop as it would be stored: bottom-anchored props sit on the floor, outer-wall props face outwards, wall props stay clear of the ends.
        public BObject makeProp(BuildingPropDef def, BuildingFace face, uint faceRef, float u, float v, uint id)
        {
            var o = new BObject { id = id, proto = def.id, face = face, faceRef = faceRef, u = u, v = def.anchorBottom ? 0f : v };
            if (def.surface == PropSurface.Wall)
            {
                BEdge e = edge(faceRef);
                if (e != null && def.outerOnly)
                    o.face = outerSide(e);
                if (e != null)
                    o.u = clampWallU(def, e, o.u);
            }
            return o;
        }

        public string placeProp(PropPrototypeId proto, BuildingFace face, uint faceRef, float u, float v)
        {
            BuildingPropDef def = BuildingProps.get(proto);
            if (def == null || !def.placeable)
                return "Unknown prop.";

            BObject o = makeProp(def, face, faceRef, u, v, (newPropId ?? newId)());
            string problem = propProblem(o, objects);
            if (problem != null)
                return problem;

            objects.Add(o);
            return finish();
        }

        public string deleteObject(uint objectId)
        {
            BObject o = objects.Find(ob => ob.id == objectId);
            if (o == null) return "That prop no longer exists.";
            objects.Remove(o);
            return finish();
        }

        public string setHeights(float floorY, float newHeight)
        {
            position.y = floorY;
            height = newHeight;
            return finish();
        }

        // Keeps every prop consistent with its face after an edit. Props follow their face through u and v; props whose face is gone,
        // or which no longer fit, are moved if they must exist (the exterior door) and dropped otherwise.
        public void fixObjects()
        {
            var kept = new List<BObject>();

            foreach (BObject o in objects.ToList())
            {
                BuildingPropDef def = o.def;
                if (def == null)
                    continue;

                if (def.surface != PropSurface.Wall)
                {
                    BArea a = area(o.faceRef);
                    if (a == null)
                        continue;

                    if (propProblem(o, null) != null)
                    {
                        var c = areaCentroid(a);
                        (o.u, o.v) = areaUv(a, c.x, c.z);
                        if (propProblem(o, null) != null && !def.essential)
                            continue;
                    }
                }
                else
                {
                    BEdge e = edge(o.faceRef);
                    if (e != null && def.outerOnly && isEdgeOutside(e.id))
                        o.face = outerSide(e);

                    if (e == null || propProblem(o, null) != null)
                    {
                        if (e != null)
                        {
                            o.u = clampWallU(def, e, o.u);
                            if (propProblem(o, null) == null)
                            {
                                kept.Add(o);
                                continue;
                            }
                        }
                        if (!def.teleports || !relocate(o, kept))
                            continue;
                    }
                }
                kept.Add(o);
            }

            // Overlaps are resolved in id order; a prop the building needs wins over one it does not.
            var result = new List<BObject>();
            foreach (BObject o in kept)
            {
                BObject clash = result.FirstOrDefault(other => conflicts(o, other));
                if (clash == null)
                {
                    result.Add(o);
                    continue;
                }
                if (o.def.essential && !clash.def.essential)
                {
                    result.Remove(clash);
                    result.Add(o);
                }
                else if (o.def.essential)
                    result.Add(o);
            }

            objects = result;
        }

        // Moves a wall prop that needs an outer wall to the first outer wall with room for it, centred.
        private bool relocate(BObject o, List<BObject> kept)
        {
            foreach (BEdge e in edges.OrderBy(e => e.id))
            {
                if (!isEdgeOutside(e.id))
                    continue;
                o.faceRef = e.id;
                o.face = outerSide(e);
                o.u = 0.5f;
                o.v = 0f;
                if (propProblem(o, kept) == null)
                    return true;
            }
            return false;
        }

        // The prop on a face closest to a point, for removing the one the player aims at.
        public BObject objectById(uint id) => objects.Find(o => o.id == id);
    }
}
