using System;
using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Moves a building between the world's slabs and a BuildingModel.
    public static unsafe class BuildingStore
    {
        public static BuildingModel load(World world, uint buildingId, Func<uint> newId = null, Func<uint> newPropId = null)
        {
            Building* b = world.buildings.find(buildingId);
            if (b == null)
                return null;

            var m = new BuildingModel { id = buildingId, position = b->position, yaw = b->yaw, height = b->height, groundY = b->groundY, newId = newId, newPropId = newPropId };

            for (int i = 0; i < world.buildingVertices.slabs.size; i++)
            {
                BuildingVertexData* v = world.buildingVertices.slabs.get(i);
                if (v->slabHeader.allocated && v->building == buildingId)
                    m.vertices.Add(new BVertex { id = v->slabHeader._playerIndexAndGeneration, x = v->x, z = v->z });
            }

            for (int i = 0; i < world.buildingEdges.slabs.size; i++)
            {
                BuildingEdgeData* e = world.buildingEdges.slabs.get(i);
                if (e->slabHeader.allocated && e->building == buildingId)
                    m.edges.Add(new BEdge { id = e->slabHeader._playerIndexAndGeneration, a = e->vertexA, b = e->vertexB, type = e->type });
            }

            for (int i = 0; i < world.buildingAreas.slabs.size; i++)
            {
                BuildingAreaData* a = world.buildingAreas.slabs.get(i);
                if (!a->slabHeader.allocated || a->building != buildingId)
                    continue;

                var area = new BArea { id = a->slabHeader._playerIndexAndGeneration, type = a->type };
                for (int k = 0; k < a->edgeCount; k++)
                    area.edges.Add(a->edges[k]);
                m.areas.Add(area);
            }

            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* p = world.props.slabs.get(i);
                if (p->slabHeader.allocated && p->building == buildingId)
                    m.objects.Add(new BObject { id = p->slabHeader._playerIndexAndGeneration, proto = p->prototypeId, face = p->face, faceRef = p->faceRef, u = p->faceU, v = p->faceV, yaw = p->faceYaw });
            }

            // Slab order depends on allocation history, so lists are put in id order to make every peer iterate identically.
            m.vertices.Sort((p, q) => p.id.CompareTo(q.id));
            m.edges.Sort((p, q) => p.id.CompareTo(q.id));
            m.areas.Sort((p, q) => p.id.CompareTo(q.id));
            m.objects.Sort((p, q) => p.id.CompareTo(q.id));
            return m;
        }

        public static void save(World world, BuildingModel m)
        {
            Building* b = world.buildings.find(m.id);
            if (b == null)
            {
                b = world.buildings.create(m.id);
            }
            b->position = m.position;
            b->yaw = m.yaw;
            b->height = m.height;
            b->groundY = m.groundY;

            var vertexIds = new HashSet<uint>();
            foreach (BVertex v in m.vertices) vertexIds.Add(v.id);
            sync(world.buildingVertices, m.id, vertexIds);
            foreach (BVertex v in m.vertices)
            {
                BuildingVertexData* d = world.buildingVertices.find(v.id);
                if (d == null) { d = world.buildingVertices.create(v.id); }
                d->building = m.id;
                d->x = v.x;
                d->z = v.z;
            }

            var edgeIds = new HashSet<uint>();
            foreach (BEdge e in m.edges) edgeIds.Add(e.id);
            sync(world.buildingEdges, m.id, edgeIds);
            foreach (BEdge e in m.edges)
            {
                BuildingEdgeData* d = world.buildingEdges.find(e.id);
                if (d == null) { d = world.buildingEdges.create(e.id); }
                d->building = m.id;
                d->vertexA = e.a;
                d->vertexB = e.b;
                d->type = e.type;
            }

            var areaIds = new HashSet<uint>();
            foreach (BArea a in m.areas) areaIds.Add(a.id);
            sync(world.buildingAreas, m.id, areaIds);
            foreach (BArea a in m.areas)
            {
                BuildingAreaData* d = world.buildingAreas.find(a.id);
                if (d == null) { d = world.buildingAreas.create(a.id); }
                d->building = m.id;
                d->type = a.type;
                d->edgeCount = a.edges.Count;
                for (int k = 0; k < 4; k++)
                    d->edges[k] = k < a.edges.Count ? a.edges[k] : 0;
            }

            saveProps(world, m);

            world.data.dirty = true;
        }

        private static Prop* findProp(World world, uint id)
        {
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* p = world.props.slabs.get(i);
                if (p->slabHeader.allocated && p->slabHeader._playerIndexAndGeneration == id)
                    return p;
            }
            return null;
        }

        // Building props are ordinary world props whose position and rotation are derived from their face.
        private static void saveProps(World world, BuildingModel m)
        {
            var keep = new HashSet<uint>();
            foreach (BObject o in m.objects) keep.Add(o.id);

            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* p = world.props.slabs.get(i);
                if (p->slabHeader.allocated && p->building == m.id && !keep.Contains(p->slabHeader._playerIndexAndGeneration))
                    PropLogic.deleteProp(world, p);
            }

            Quaternion rotation = Quaternion.Euler(0f, m.yaw, 0f);
            foreach (BObject o in m.objects)
            {
                Prop* p = findProp(world, o.id);
                if (p == null)
                {
                    PropHandle handle = world.props.allocate(EntityId.playerIndex(o.id), EntityId.counter(o.id));
                    p = world.get(handle);
                }

                p->prototypeId = o.proto;
                p->building = m.id;
                p->face = o.face;
                p->faceRef = o.faceRef;
                p->faceU = o.u;
                p->faceV = o.v;
                p->faceYaw = o.yaw;
                p->position = m.position + rotation * m.objectLocalPosition(o);
                p->eulerRotation = new Vector3(0f, m.yaw + m.objectLocalYaw(o), 0f);
            }
        }

        // Every owned entity begins with its slab header followed by the owning building id.
        private static uint ownerOf<T>(T* item) where T : unmanaged => *(uint*)((byte*)item + sizeof(SlabHeader));

        // Frees every entity of the building that the model no longer contains.
        private static void sync<T>(EntitySlab<T> slab, uint buildingId, HashSet<uint> keep) where T : unmanaged
        {
            for (int i = 0; i < slab.slabs.size; i++)
            {
                T* item = slab.slabs.get(i);
                SlabHeader* header = (SlabHeader*)item;
                if (header->allocated && ownerOf(item) == buildingId && !keep.Contains(header->_playerIndexAndGeneration))
                    slab.remove(item);
            }
        }

        // Removes a building and everything it owns.
        public static void remove(World world, uint buildingId)
        {
            removeOwned(world.buildingVertices, buildingId);
            removeOwned(world.buildingEdges, buildingId);
            removeOwned(world.buildingAreas, buildingId);
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* p = world.props.slabs.get(i);
                if (p->slabHeader.allocated && p->building == buildingId)
                    PropLogic.deleteProp(world, p);
            }
            Building* b = world.buildings.find(buildingId);
            if (b != null)
                world.buildings.remove(b);
            world.data.dirty = true;
        }

        private static void removeOwned<T>(EntitySlab<T> slab, uint buildingId) where T : unmanaged
        {
            for (int i = 0; i < slab.slabs.size; i++)
            {
                T* item = slab.slabs.get(i);
                if (((SlabHeader*)item)->allocated && ownerOf(item) == buildingId)
                    slab.remove(item);
            }
        }
    }
}
