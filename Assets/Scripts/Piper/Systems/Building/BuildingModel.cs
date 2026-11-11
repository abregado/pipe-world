using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PipeWorld.Piper
{
    public class BVertex { public uint id; public float x, z; }

    public class BEdge { public uint id, a, b; public byte type; }

    public class BArea { public uint id; public List<uint> edges = new List<uint>(); public byte type; }

    // A building's footprint, edited as ordinary managed lists. Loaded from the world's slabs, changed, validated and written back,
    // so every edit is validated in one place and a rejected edit never touches the world.
    public partial class BuildingModel
    {
        public uint id;
        public Vector3 position;
        public float yaw;
        public float height = BuildingConstants.CeilingHeight;
        public float groundY;
        public List<BVertex> vertices = new List<BVertex>();
        public List<BEdge> edges = new List<BEdge>();
        public List<BArea> areas = new List<BArea>();
        public List<BObject> objects = new List<BObject>();

        // Supplies ids for new entities; set by whoever loads the model.
        public Func<uint> newId;
        // Supplies ids for new props, which come from the placing player's prop counter.
        public Func<uint> newPropId;

        public BuildingModel Clone()
        {
            var c = new BuildingModel { id = id, position = position, yaw = yaw, height = height, groundY = groundY, newId = newId, newPropId = newPropId };
            c.vertices = vertices.Select(v => new BVertex { id = v.id, x = v.x, z = v.z }).ToList();
            c.edges = edges.Select(e => new BEdge { id = e.id, a = e.a, b = e.b, type = e.type }).ToList();
            c.areas = areas.Select(a => new BArea { id = a.id, edges = new List<uint>(a.edges), type = a.type }).ToList();
            c.objects = objects.Select(o => new BObject { id = o.id, proto = o.proto, face = o.face, faceRef = o.faceRef, u = o.u, v = o.v, yaw = o.yaw }).ToList();
            return c;
        }

        // ---- Lookups ----

        public BVertex vertex(uint vid) => vertices.Find(v => v.id == vid);
        public BEdge edge(uint eid) => edges.Find(e => e.id == eid);
        public BArea area(uint aid) => areas.Find(a => a.id == aid);

        // ---- Queries (builder-proto model.ts) ----

        public (double x, double z) edgeMidpoint(BEdge e)
        {
            BVertex a = vertex(e.a), b = vertex(e.b);
            return ((a.x + b.x) / 2.0, (a.z + b.z) / 2.0);
        }

        public (double x, double z) edgeNormal(BEdge e, BArea outwardFrom = null)
        {
            BVertex a = vertex(e.a), b = vertex(e.b);
            double dx = b.x - a.x, dz = b.z - a.z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            double nx = -dz / len, nz = dx / len;

            if (outwardFrom != null)
            {
                var c = areaCentroid(outwardFrom);
                var m = edgeMidpoint(e);
                if (nx * (c.x - m.x) + nz * (c.z - m.z) > 0)
                {
                    nx = -nx;
                    nz = -nz;
                }
            }
            return (nx, nz);
        }

        public double edgeLength(BEdge e)
        {
            BVertex a = vertex(e.a), b = vertex(e.b);
            return BuildingGeometry.dist(a.x, a.z, b.x, b.z);
        }

        public List<uint> areaVertexIds(BArea area)
        {
            var ids = new List<uint>();
            foreach (uint eid in area.edges)
            {
                BEdge e = edge(eid);
                if (!ids.Contains(e.a)) ids.Add(e.a);
                if (!ids.Contains(e.b)) ids.Add(e.b);
            }
            return ids;
        }

        public (double x, double z) areaCentroid(BArea area)
        {
            List<uint> ids = areaVertexIds(area);
            double cx = 0, cz = 0;
            foreach (uint vid in ids)
            {
                BVertex v = vertex(vid);
                cx += v.x;
                cz += v.z;
            }
            return (cx / ids.Count, cz / ids.Count);
        }

        // Vertices ordered by angle around the centroid.
        public List<BVertex> areaVertices(BArea area)
        {
            List<BVertex> verts = areaVertexIds(area).Select(vertex).ToList();
            double cx = verts.Average(v => (double)v.x), cz = verts.Average(v => (double)v.z);
            verts.Sort((p, q) => Math.Atan2(p.z - cz, p.x - cx).CompareTo(Math.Atan2(q.z - cz, q.x - cx)));
            return verts;
        }

        public List<BArea> areasForEdge(uint eid) => areas.Where(a => a.edges.Contains(eid)).ToList();

        public static bool isTriangle(BArea a) => a.edges.Count == 3;

        public bool edgeBelongsToTriangle(uint eid) => areasForEdge(eid).Any(isTriangle);

        public List<BArea> areasForVertex(uint vid)
        {
            return areas.Where(a => a.edges.Any(eid => { BEdge e = edge(eid); return e.a == vid || e.b == vid; })).ToList();
        }

        public List<BVertex> triangleOnlyVertices()
        {
            return vertices.Where(v =>
            {
                List<BArea> list = areasForVertex(v.id);
                return list.Count > 0 && list.All(isTriangle);
            }).ToList();
        }

        public List<(double x, double z)> areaPolygon(BArea area) => areaVertices(area).Select(v => ((double)v.x, (double)v.z)).ToList();

        public BArea areaAt(double x, double z)
        {
            BArea best = null;
            double bestSize = double.MaxValue;
            foreach (BArea a in areas)
            {
                var poly = areaPolygon(a);
                if (!BuildingGeometry.pointInPolygon(x, z, poly)) continue;
                double s = Math.Abs(BuildingGeometry.signedPolyArea(poly));
                if (s < bestSize)
                {
                    bestSize = s;
                    best = a;
                }
            }
            return best;
        }

        public bool isEdgeInside(uint eid) => areasForEdge(eid).Count >= 2;

        public bool isEdgeOutside(uint eid) => areasForEdge(eid).Count == 1;

        public bool canDeleteArea(BArea area)
        {
            if (areas.Count <= 1) return false;
            int shared = area.edges.Count(eid => areasForEdge(eid).Count > 1);
            return shared == 1;
        }

        public List<uint> collinearChain(uint edgeId)
        {
            BEdge e = edge(edgeId);
            BVertex a = vertex(e.a), b = vertex(e.b);
            double edx = b.x - a.x, edz = b.z - a.z;
            double elen = Math.Sqrt(edx * edx + edz * edz);
            if (elen == 0) return new List<uint> { edgeId };
            double enx = edx / elen, enz = edz / elen;

            var chain = new HashSet<uint> { edgeId };
            var queue = new Stack<uint>();
            queue.Push(edgeId);

            while (queue.Count > 0)
            {
                BEdge cur = edge(queue.Pop());
                foreach (BEdge other in edges)
                {
                    if (chain.Contains(other.id)) continue;
                    if (other.a != cur.a && other.a != cur.b && other.b != cur.a && other.b != cur.b) continue;
                    if (edgeBelongsToTriangle(other.id) && !areasForEdge(other.id).Any(ar => !isTriangle(ar))) continue;
                    BVertex oa = vertex(other.a), ob = vertex(other.b);
                    double odx = ob.x - oa.x, odz = ob.z - oa.z;
                    double olen = Math.Sqrt(odx * odx + odz * odz);
                    if (olen == 0) continue;
                    double crossV = enx * (odz / olen) - enz * (odx / olen);
                    if (Math.Abs(crossV) < 1e-6)
                    {
                        chain.Add(other.id);
                        queue.Push(other.id);
                    }
                }
            }
            return chain.ToList();
        }

        public List<uint> chainVertexIds(List<uint> chainEdges)
        {
            var ids = new HashSet<uint>();
            foreach (uint eid in chainEdges)
            {
                BEdge e = edge(eid);
                ids.Add(e.a);
                ids.Add(e.b);
            }
            return ids.ToList();
        }

        // ---- Validation (builder-proto validation.ts) ----

        public bool allQuadsValid()
        {
            foreach (BArea a in areas)
            {
                if (isTriangle(a)) continue;
                List<BVertex> verts = areaVertices(a);
                if (verts.Count < 4) return false;
                if (Math.Abs(BuildingGeometry.signedPolyArea(verts.Select(v => ((double)v.x, (double)v.z)).ToList())) < 1e-6) return false;
            }
            return true;
        }

        private List<(double ax, double az, double bx, double bz)> areaSegments(BArea area)
        {
            var poly = areaPolygon(area);
            var segs = new List<(double, double, double, double)>();
            for (int i = 0; i < poly.Count; i++)
            {
                int j = (i + 1) % poly.Count;
                segs.Add((poly[i].x, poly[i].z, poly[j].x, poly[j].z));
            }
            return segs;
        }

        private bool areasOverlap(BArea a, BArea b)
        {
            foreach (var sa in areaSegments(a))
                foreach (var sb in areaSegments(b))
                    if (BuildingGeometry.segmentsIntersect(sa.ax, sa.az, sa.bx, sa.bz, sb.ax, sb.az, sb.bx, sb.bz))
                        return true;

            if (!a.edges.Any(eid => b.edges.Contains(eid)))
            {
                var polyA = areaPolygon(a);
                var polyB = areaPolygon(b);
                if (BuildingGeometry.pointInPolygon(polyA[0].x, polyA[0].z, polyB)) return true;
                if (BuildingGeometry.pointInPolygon(polyB[0].x, polyB[0].z, polyA)) return true;
            }
            return false;
        }

        // Returns null when the footprint is valid, otherwise the reason it is not.
        public string validate()
        {
            for (int i = 0; i < areas.Count; i++)
                for (int j = i + 1; j < areas.Count; j++)
                    if (areasOverlap(areas[i], areas[j]))
                        return "Rooms would overlap.";

            for (int i = 0; i < edges.Count; i++)
            {
                BVertex ai = vertex(edges[i].a), bi = vertex(edges[i].b);
                if (edgeLength(edges[i]) < BuildingConstants.MinEdgeLength)
                    return "A wall would be shorter than half a metre.";
                for (int j = i + 1; j < edges.Count; j++)
                {
                    BVertex aj = vertex(edges[j].a), bj = vertex(edges[j].b);
                    if (BuildingGeometry.segmentsOverlapCollinear(ai.x, ai.z, bi.x, bi.z, aj.x, aj.z, bj.x, bj.z))
                        return "Walls would overlap.";
                }
            }

            foreach (BVertex v in vertices)
            {
                if (Math.Abs(v.x) > BuildingConstants.MaxExtent || Math.Abs(v.z) > BuildingConstants.MaxExtent)
                    return "The building would be too large.";
            }

            if (height < BuildingConstants.MinHeight || height > BuildingConstants.MaxHeight)
                return "The ceiling height must be between " + BuildingConstants.MinHeight + " and " + BuildingConstants.MaxHeight + " m.";
            if (position.y < groundY - 0.001f || position.y > groundY + BuildingConstants.MaxRaise)
                return "The floor cannot go below the ground or more than " + BuildingConstants.MaxRaise + " m above it.";

            if (!objects.Any(o => o.proto == PropPrototypeId.ExteriorDoor))
                return "The building needs an exterior door.";
            if (!objects.Any(o => o.proto == PropPrototypeId.Terminal))
                return "The building needs a terminal.";

            return null;
        }
    }
}
