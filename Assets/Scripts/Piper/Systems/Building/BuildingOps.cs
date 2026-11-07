using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PipeWorld.Piper
{
    public class SliceInfo
    {
        public BArea area;
        public bool isTriangle;
        public (double x, double z) pointA, pointB;
        public uint edgeA, edgeB;
        public uint vertexA;
        // The whole cut: where it crosses each wall (the first is on the outer wall it starts from), the walls crossed and the rooms between.
        public List<(double x, double z)> points = new List<(double x, double z)>();
        public List<uint> crossEdges = new List<uint>();
        public List<BArea> rooms = new List<BArea>();
    }

    // The edit operations of builder-proto's modes. Each changes this model and returns null on success or the reason it is not allowed.
    // Callers run them on a clone, so a rejected edit leaves nothing behind.
    public partial class BuildingModel
    {
        // Ids for previews and trial edits, which are discarded and so only need to be distinct within one model.
        public static Func<uint> trialIds()
        {
            uint next = 0xFF0000u;
            return () => next++;
        }

        public static BuildingModel createDefault(Vector3 position, float yaw, float height, float groundY, Func<uint> newId, Func<uint> newPropId)
        {
            var m = new BuildingModel { newId = newId, newPropId = newPropId, position = position, yaw = yaw, height = height, groundY = groundY };
            m.id = newId();

            float h = BuildingConstants.StartHalfSize;
            BVertex v0 = m.addVertex(-h, -h), v1 = m.addVertex(h, -h), v2 = m.addVertex(h, h), v3 = m.addVertex(-h, h);
            BEdge south = m.addEdge(v0.id, v1.id, 1), east = m.addEdge(v1.id, v2.id, 1), north = m.addEdge(v2.id, v3.id, 1), west = m.addEdge(v3.id, v0.id, 1);
            BArea room = m.addArea(new[] { south.id, east.id, north.id, west.id });

            Func<uint> propId = newPropId ?? newId;
            m.objects.Add(new BObject { id = propId(), proto = PropPrototypeId.ExteriorDoor, face = m.outerSide(south), faceRef = south.id, u = 0.5f, v = 0f });
            var (tu, tv) = m.areaUv(room, 0, h - 0.7f);
            m.objects.Add(new BObject { id = propId(), proto = PropPrototypeId.Terminal, face = BuildingFace.Floor, faceRef = room.id, u = tu, v = tv });
            var (cu, cv) = m.areaUv(room, -h + 0.5f, h - 0.7f);
            m.objects.Add(new BObject { id = propId(), proto = PropPrototypeId.CargoTerminal, face = BuildingFace.Floor, faceRef = room.id, u = cu, v = cv });
            return m;
        }

        private BVertex addVertex(double x, double z)
        {
            var v = new BVertex { id = newId(), x = (float)x, z = (float)z };
            vertices.Add(v);
            return v;
        }

        private BEdge addEdge(uint a, uint b, byte type)
        {
            var e = new BEdge { id = newId(), a = a, b = b, type = type };
            edges.Add(e);
            return e;
        }

        private BArea addArea(IEnumerable<uint> edgeIds, byte type = 0)
        {
            var a = new BArea { id = newId(), edges = edgeIds.ToList(), type = type };
            areas.Add(a);
            return a;
        }

        private string finish()
        {
            fixObjects();
            return validate();
        }

        // ---- Manipulate ----

        public string moveEdge(uint edgeId, float offset)
        {
            BEdge e = edge(edgeId);
            if (e == null) return "That wall no longer exists.";
            if (edgeBelongsToTriangle(edgeId)) return "Walls of a triangular room cannot be moved.";
            if (Math.Abs(offset) < 1e-4f) return "Nothing to move.";

            var n = edgeNormal(e);
            foreach (uint vid in chainVertexIds(collinearChain(edgeId)))
            {
                BVertex v = vertex(vid);
                v.x = (float)(v.x + n.x * offset);
                v.z = (float)(v.z + n.z * offset);
            }

            if (!allQuadsValid()) return "A room would stop being a quadrilateral.";
            return finish();
        }

        public string moveVertex(uint vertexId, float x, float z)
        {
            BVertex v = vertex(vertexId);
            if (v == null) return "That corner no longer exists.";
            if (!triangleOnlyVertices().Any(t => t.id == vertexId)) return "Only the corners of triangular rooms move freely.";
            v.x = x;
            v.z = z;
            return finish();
        }

        // ---- Extrude ----

        public string extrude(uint edgeId, float offset)
        {
            BEdge e = edge(edgeId);
            if (e == null) return "That wall no longer exists.";
            List<BArea> owners = areasForEdge(edgeId);
            if (owners.Count != 1) return "Only an outer wall can be extruded.";
            if (offset < 0.05f) return "Extrude outwards.";

            var n = edgeNormal(e, owners[0]);
            BVertex oldA = vertex(e.a), oldB = vertex(e.b);
            BVertex v1 = addVertex(oldA.x + n.x * offset, oldA.z + n.z * offset);
            BVertex v2 = addVertex(oldB.x + n.x * offset, oldB.z + n.z * offset);
            BEdge side1 = addEdge(oldA.id, v1.id, 1), top = addEdge(v1.id, v2.id, 1), side2 = addEdge(oldB.id, v2.id, 1);
            addArea(new[] { e.id, side1.id, top.id, side2.id });
            return finish();
        }

        // ---- Create Tri ----

        public string createTri(uint edgeId, float x, float z)
        {
            BEdge e = edge(edgeId);
            if (e == null) return "That wall no longer exists.";
            if (areasForEdge(edgeId).Count != 1) return "Only an outer wall can grow a triangle.";

            BVertex va = vertex(e.a), vb = vertex(e.b);
            if ((x == va.x && z == va.z) || (x == vb.x && z == vb.z)) return "The point is on the wall.";

            BVertex nv = addVertex(x, z);
            BEdge e1 = addEdge(va.id, nv.id, 1), e2 = addEdge(vb.id, nv.id, 1);
            addArea(new[] { e.id, e1.id, e2.id });
            return finish();
        }

        // ---- Slice ----

        public SliceInfo computeSlice(BArea area, double wx, double wz)
        {
            if (isTriangle(area))
            {
                List<BVertex> verts = areaVertices(area);
                BVertex far = verts.OrderByDescending(v => BuildingGeometry.dist(wx, wz, v.x, v.z)).First();

                BEdge opposite = null;
                foreach (uint eid in area.edges)
                {
                    BEdge e = edge(eid);
                    if (e.a != far.id && e.b != far.id && areasForEdge(eid).Count == 1)
                    {
                        opposite = e;
                        break;
                    }
                }
                if (opposite == null) return null;

                BVertex oa = vertex(opposite.a), ob = vertex(opposite.b);
                BuildingGeometry.closestPointOnSeg(wx, wz, oa.x, oa.z, ob.x, ob.z, out double cx, out double cz, out double t);
                if (t < 0.05 || t > 0.95) return null;

                var triangleCut = new SliceInfo { area = area, isTriangle = true, pointA = (far.x, far.z), pointB = (cx, cz), edgeA = 0, edgeB = opposite.id, vertexA = far.id };
                triangleCut.points.Add((far.x, far.z));
                triangleCut.points.Add((cx, cz));
                triangleCut.crossEdges.Add(0);
                triangleCut.crossEdges.Add(opposite.id);
                triangleCut.rooms.Add(area);
                return triangleCut;
            }

            uint closest = 0;
            double closestDist = double.MaxValue;
            foreach (uint eid in area.edges)
            {
                if (areasForEdge(eid).Count > 1) continue;
                BEdge e = edge(eid);
                BVertex a = vertex(e.a), b = vertex(e.b);
                double d = BuildingGeometry.distPointToSegment(wx, wz, a.x, a.z, b.x, b.z);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = eid;
                }
            }
            if (closest == 0) return null;

            BEdge src = edge(closest);
            BVertex sa = vertex(src.a), sb = vertex(src.b);
            BuildingGeometry.closestPointOnSeg(wx, wz, sa.x, sa.z, sb.x, sb.z, out double px, out double pz, out double st);
            if (st < 0.05 || st > 0.95) return null;

            var n = edgeNormal(src, area);
            double inx = -n.x, inz = -n.z;

            // The cut runs straight in from the outer wall, through as many rooms as it crosses, until it reaches an outer wall again.
            // Every room it passes must be a four-sided room that it crosses from one wall to the opposite one, so no triangle or
            // five-sided room is created, and it must not meet a corner or a triangular room.
            var cut = new SliceInfo { area = area, isTriangle = false, pointA = (px, pz), edgeA = closest };
            cut.points.Add((px, pz));
            cut.crossEdges.Add(closest);

            BArea room = area;
            uint entry = closest;
            double lastT = 0;
            for (int guard = 0; guard < 24; guard++)
            {
                if (room.edges.Count != 4)
                    return null;

                double bestT = double.MaxValue;
                uint hitEdge = 0;
                foreach (uint eid in room.edges)
                {
                    if (eid == entry) continue;
                    BEdge e = edge(eid);
                    BVertex ea = vertex(e.a), eb = vertex(e.b);
                    double? hitT = BuildingGeometry.raySegIntersect(px, pz, inx, inz, ea.x, ea.z, eb.x, eb.z);
                    if (hitT.HasValue && hitT.Value > lastT + 1e-6 && hitT.Value < bestT)
                    {
                        bestT = hitT.Value;
                        hitEdge = eid;
                    }
                }
                if (hitEdge == 0)
                    return null;

                BEdge exit = edge(hitEdge), through = edge(entry);
                if (exit.a == through.a || exit.a == through.b || exit.b == through.a || exit.b == through.b)
                    return null;

                BVertex xa = vertex(exit.a), xb = vertex(exit.b);
                (double x, double z) point = (px + inx * bestT, pz + inz * bestT);
                BuildingGeometry.closestPointOnSeg(point.x, point.z, xa.x, xa.z, xb.x, xb.z, out _, out _, out double exitT);
                if (exitT < 0.05 || exitT > 0.95)
                    return null;

                cut.rooms.Add(room);
                cut.points.Add(point);
                cut.crossEdges.Add(hitEdge);

                List<BArea> sharing = areasForEdge(hitEdge);
                if (sharing.Count <= 1)
                    break;

                room = sharing.First(a => a.id != room.id);
                entry = hitEdge;
                lastT = bestT;
            }

            if (cut.rooms.Count == 0 || cut.crossEdges.Count != cut.rooms.Count + 1)
                return null;

            cut.edgeB = cut.crossEdges[cut.crossEdges.Count - 1];
            cut.pointB = cut.points[cut.points.Count - 1];
            return cut;
        }

        // Splits an edge at a point; wall objects on it move to whichever half now holds them.
        private (BVertex vertex, BEdge first, BEdge second) splitEdge(uint edgeId, (double x, double z) point)
        {
            BEdge e = edge(edgeId);
            BVertex a = vertex(e.a), b = vertex(e.b);
            BuildingGeometry.closestPointOnSeg(point.x, point.z, a.x, a.z, b.x, b.z, out _, out _, out double splitT);

            BVertex nv = addVertex(point.x, point.z);
            BEdge first = addEdge(e.a, nv.id, e.type), second = addEdge(nv.id, e.b, e.type);

            foreach (BArea area in areas)
            {
                int index = area.edges.IndexOf(edgeId);
                if (index != -1)
                {
                    area.edges.RemoveAt(index);
                    area.edges.InsertRange(index, new[] { first.id, second.id });
                }
            }

            // Wall props keep their side and their distance along the wall, now measured on whichever half holds them.
            foreach (BObject o in objects.Where(o => o.def.surface == PropSurface.Wall && o.faceRef == edgeId))
            {
                if (o.u < splitT)
                {
                    o.faceRef = first.id;
                    o.u = (float)(o.u / splitT);
                }
                else
                {
                    o.faceRef = second.id;
                    o.u = (float)((o.u - splitT) / (1 - splitT));
                }
            }

            edges.Remove(e);
            return (nv, first, second);
        }

        private List<uint> walkPolygon(BArea area)
        {
            List<BEdge> es = area.edges.Select(edge).ToList();
            var ordered = new List<uint> { es[0].a };
            var used = new HashSet<uint>();

            for (int step = 0; step < es.Count; step++)
            {
                uint last = ordered[ordered.Count - 1];
                foreach (BEdge e in es)
                {
                    if (used.Contains(e.id)) continue;
                    if (e.a == last) { ordered.Add(e.b); used.Add(e.id); break; }
                    if (e.b == last) { ordered.Add(e.a); used.Add(e.id); break; }
                }
            }
            if (ordered.Count > 1 && ordered[ordered.Count - 1] == ordered[0]) ordered.RemoveAt(ordered.Count - 1);
            return ordered;
        }

        public string slice(uint areaId, float wx, float wz)
        {
            BArea target = area(areaId);
            if (target == null) return "That room no longer exists.";
            SliceInfo info = computeSlice(target, wx, wz);
            if (info == null) return "There is nowhere to slice here.";

            // Every wall the cut crosses is split at the crossing; the cut is then a new wall through each room it passes.
            var roomIds = info.rooms.Select(r => r.id).ToList();
            var cutVertices = new List<uint>();
            for (int k = 0; k < info.crossEdges.Count; k++)
            {
                if (info.isTriangle && k == 0)
                {
                    cutVertices.Add(info.vertexA);
                    continue;
                }
                cutVertices.Add(splitEdge(info.crossEdges[k], info.points[k]).vertex.id);
            }

            for (int k = 0; k < roomIds.Count; k++)
            {
                string failed = cutRoom(roomIds[k], cutVertices[k], cutVertices[k + 1]);
                if (failed != null)
                    return failed;
            }
            return finish();
        }

        // Divides one room in two with a new wall between two of its corners.
        private string cutRoom(uint roomId, uint vertA, uint vertB)
        {
            BArea target = area(roomId);
            BEdge sliceEdge = addEdge(vertA, vertB, 0);
            List<uint> poly = walkPolygon(target);
            int ia = poly.IndexOf(vertA), ib = poly.IndexOf(vertB);
            if (ia == -1 || ib == -1) return "The slice failed.";

            List<uint> half1 = loop(poly, ia, ib), half2 = loop(poly, ib, ia);
            var candidates = target.edges.Concat(new[] { sliceEdge.id }).ToList();
            List<uint> edges1 = edgesForLoop(half1, candidates), edges2 = edgesForLoop(half2, candidates);

            // Props on the room's floor and ceiling stay where they are and move to whichever half holds them.
            var onTarget = objects.Where(o => o.def.surface != PropSurface.Wall && o.faceRef == target.id)
                .Select(o => (prop: o, point: areaPoint(target, o.u, o.v))).ToList();

            areas.Remove(target);
            addArea(edges1, target.type);
            addArea(edges2, target.type);
            foreach (var entry in onTarget)
            {
                BArea now = areaAt(entry.point.x, entry.point.z);
                if (now == null) { objects.Remove(entry.prop); continue; }
                entry.prop.faceRef = now.id;
                (entry.prop.u, entry.prop.v) = areaUv(now, entry.point.x, entry.point.z);
            }
            return null;
        }

        private static List<uint> loop(List<uint> poly, int from, int to)
        {
            var result = new List<uint> { poly[from] };
            int i = from;
            while (i != to)
            {
                i = (i + 1) % poly.Count;
                result.Add(poly[i]);
            }
            return result;
        }

        private List<uint> edgesForLoop(List<uint> loopVerts, List<uint> candidates)
        {
            var result = new List<uint>();
            for (int k = 0; k < loopVerts.Count; k++)
            {
                uint v1 = loopVerts[k], v2 = loopVerts[(k + 1) % loopVerts.Count];
                foreach (uint eid in candidates)
                {
                    BEdge e = edge(eid);
                    if ((e.a == v1 && e.b == v2) || (e.b == v1 && e.a == v2))
                    {
                        result.Add(eid);
                        break;
                    }
                }
            }
            return result;
        }

        // ---- Delete Area ----

        public string deleteArea(uint areaId)
        {
            BArea target = area(areaId);
            if (target == null) return "That room no longer exists.";
            if (!canDeleteArea(target)) return "Only a room attached by a single wall can be deleted.";

            areas.Remove(target);
            var usedEdges = new HashSet<uint>(areas.SelectMany(a => a.edges));
            edges.RemoveAll(e => !usedEdges.Contains(e.id));
            var usedVertices = new HashSet<uint>(edges.SelectMany(e => new[] { e.a, e.b }));
            vertices.RemoveAll(v => !usedVertices.Contains(v.id));
            return finish();
        }

        public BEdge nearestEdge(double x, double z)
        {
            BEdge best = null;
            double bestD = double.MaxValue;
            foreach (BEdge e in edges)
            {
                BVertex a = vertex(e.a), b = vertex(e.b);
                double d = BuildingGeometry.distPointToSegment(x, z, a.x, a.z, b.x, b.z);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }
    }
}
