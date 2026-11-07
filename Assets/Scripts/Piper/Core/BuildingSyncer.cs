using System;
using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Keeps one BuildingView per building and rebuilds a view when its data changes. A content hash detects the change, so edits
    // arriving as actions, as snapshots or as loaded saves are all handled the same way.
    public unsafe class BuildingSyncer
    {
        private readonly Dictionary<uint, BuildingView> views = new Dictionary<uint, BuildingView>();
        private readonly Dictionary<uint, ulong> hashes = new Dictionary<uint, ulong>();
        private readonly List<uint> staleScratch = new List<uint>();
        private readonly Transform parent;
        private readonly Material material;

        // Set by the edit mode so the ceiling of the building being edited stays out of the way.
        public uint editingBuilding;

        public BuildingSyncer(Transform parent)
        {
            this.parent = parent;
            material = Resources.Load<Material>("Materials/Building");
            SUtil.ReleaseAssert(material != null, "Resources/Materials/Building.mat is missing");
        }

        public int viewCount => views.Count;

        public BuildingView view(uint id) => views.TryGetValue(id, out BuildingView v) ? v : null;

        public void sync(World world)
        {
            hashes.Clear();
            HashAll(world);

            staleScratch.Clear();
            foreach (uint id in views.Keys)
                staleScratch.Add(id);

            for (int i = 0; i < world.buildings.slabs.size; i++)
            {
                Building* b = world.buildings.slabs.get(i);
                if (!b->slabHeader.allocated)
                    continue;

                uint id = b->slabHeader._playerIndexAndGeneration;
                staleScratch.Remove(id);
                hashes.TryGetValue(id, out ulong hash);
                hash += Mix(id, 5, BitConverter.SingleToInt32Bits(b->position.x), BitConverter.SingleToInt32Bits(b->position.y), BitConverter.SingleToInt32Bits(b->position.z), BitConverter.SingleToInt32Bits(b->yaw), BitConverter.SingleToInt32Bits(b->height), BitConverter.SingleToInt32Bits(b->groundY));

                if (!views.TryGetValue(id, out BuildingView v))
                {
                    v = BuildingView.Create(id, parent, material);
                    views[id] = v;
                    v.contentHash = 0;
                }

                if (v.contentHash != hash)
                {
                    BuildingModel model = BuildingStore.load(world, id);
                    if (model != null && model.areas.Count > 0)
                    {
                        v.Rebuild(model);
                        v.contentHash = hash;
                    }
                }

                v.SetCeilingVisible(id != editingBuilding);
            }

            foreach (uint id in staleScratch)
            {
                UnityEngine.Object.Destroy(views[id].gameObject);
                views.Remove(id);
            }
        }

        private void Add(uint owner, ulong h)
        {
            hashes.TryGetValue(owner, out ulong current);
            hashes[owner] = current + h;
        }

        private void HashAll(World world)
        {
            for (int i = 0; i < world.buildingVertices.slabs.size; i++)
            {
                BuildingVertexData* v = world.buildingVertices.slabs.get(i);
                if (v->slabHeader.allocated)
                    Add(v->building, Mix(1, BitConverter.SingleToInt32Bits(v->x), BitConverter.SingleToInt32Bits(v->z), v->slabHeader._playerIndexAndGeneration));
            }
            for (int i = 0; i < world.buildingEdges.slabs.size; i++)
            {
                BuildingEdgeData* e = world.buildingEdges.slabs.get(i);
                if (e->slabHeader.allocated)
                    Add(e->building, Mix(2, e->vertexA, e->vertexB, e->slabHeader._playerIndexAndGeneration, e->type));
            }
            for (int i = 0; i < world.buildingAreas.slabs.size; i++)
            {
                BuildingAreaData* a = world.buildingAreas.slabs.get(i);
                if (a->slabHeader.allocated)
                    Add(a->building, Mix(3, a->edges[0], a->edges[1], a->edges[2], a->edges[3], a->edgeCount, a->type, a->slabHeader._playerIndexAndGeneration));
            }
            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* p = world.props.slabs.get(i);
                if (p->slabHeader.allocated && p->building != 0)
                    Add(p->building, Mix(4, (long)p->prototypeId, (long)p->face, p->faceRef, BitConverter.SingleToInt32Bits(p->faceU), BitConverter.SingleToInt32Bits(p->faceV), p->slabHeader._playerIndexAndGeneration));
            }
        }

        // Entity hashes are summed, so slab order does not matter.
        private static ulong Mix(params long[] values)
        {
            ulong h = 1469598103934665603UL;
            foreach (long v in values)
            {
                h ^= (ulong)v;
                h *= 1099511628211UL;
            }
            return h;
        }

        public void destroyAll()
        {
            foreach (BuildingView v in views.Values)
            {
                v.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(v.gameObject);
            }
            views.Clear();
        }
    }
}
