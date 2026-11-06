using System;
using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Keeps one pooled PropBehaviour view alive for every world prop near the player.
    public unsafe class PropSyncer
    {
        private readonly World world;
        private readonly Dictionary<PropPrototypeId, GameObjectPool<PropBehaviour>> objectPools = new Dictionary<PropPrototypeId, GameObjectPool<PropBehaviour>>();
        public readonly Dictionary<LiveKey, PropBehaviour> liveObjects = new Dictionary<LiveKey, PropBehaviour>();

        public PropSyncer(World world)
        {
            this.world = world;
        }

        public struct LiveKey : IEquatable<LiveKey>
        {
            public PropPrototypeId id;
            public Vec3f position;
            public Vec3f eulerRotation;

            public bool Equals(LiveKey other)
            {
                return id == other.id && position.Equals(other.position) && eulerRotation.Equals(other.eulerRotation);
            }

            public override bool Equals(object obj) => obj is LiveKey other && Equals(other);

            public override int GetHashCode()
            {
                return HashCode.Combine((int)id, position, eulerRotation);
            }
        }

        public void destroyAll()
        {
            foreach (PropBehaviour live in liveObjects.Values)
                UnityEngine.Object.Destroy(live.gameObject);
            liveObjects.Clear();

            foreach (GameObjectPool<PropBehaviour> pool in objectPools.Values)
                pool.destroyAll();
            objectPools.Clear();
        }

        public PropBehaviour tryGet(PropHandle propHandle)
        {
            Prop* prop = world.tryGet(propHandle);
            if (prop == null)
                return null;

            liveObjects.TryGetValue(getLiveKey(prop), out PropBehaviour propBehaviour);
            return propBehaviour;
        }

        private static LiveKey getLiveKey(Prop* prop)
        {
            return new LiveKey()
            {
                id = prop->prototypeId,
                position = prop->position,
                eulerRotation = prop->eulerRotation,
            };
        }

        private bool isStillAlive(PropBehaviour liveObject, Vector3 viewerPosition)
        {
            Prop* prop = world.tryGet(liveObject.propHandle);
            if (prop == null)
                return false;

            if (prop->prototypeId != liveObject.PropPrototypeId)
                return false;

            if (prop->position != liveObject.transform.position)
                return false;

            return ((Vector3)prop->position - viewerPosition).sqrMagnitude <= SystemSync.FADEOUT_DISTANCE * SystemSync.FADEOUT_DISTANCE;
        }

        public void sync(bool isInitialSync, Vector3 viewerPosition)
        {
            List<LiveKey> keysToRemove = null;
            foreach (KeyValuePair<LiveKey, PropBehaviour> pair in liveObjects)
            {
                if (isStillAlive(pair.Value, viewerPosition))
                    continue;

                PropBehaviour unityObject = pair.Value;
                unityObject.PrepareForPool();
                objectPools[unityObject.PropPrototypeId].destroy(unityObject);

                keysToRemove ??= new List<LiveKey>();
                keysToRemove.Add(pair.Key);
            }

            if (keysToRemove != null)
            {
                foreach (LiveKey key in keysToRemove)
                    liveObjects.Remove(key);
            }

            for (int i = 0; i < world.props.slabs.size; i++)
            {
                Prop* prop = world.props.slabs.get(i);
                if (!prop->slabHeader.allocated)
                    continue;

                float distanceSquared = ((Vector3)prop->position - viewerPosition).sqrMagnitude;
                if (distanceSquared > SystemSync.FADEIN_DISTANCE * SystemSync.FADEIN_DISTANCE)
                    continue;

                LiveKey key = getLiveKey(prop);
                if (liveObjects.ContainsKey(key))
                    continue;

                if (!objectPools.TryGetValue(prop->prototypeId, out GameObjectPool<PropBehaviour> pool))
                {
                    pool = new GameObjectPool<PropBehaviour>();
                    objectPools.Add(prop->prototypeId, pool);
                }

                PropBehaviour prefab = PropPrototypes.instance[prop->prototypeId].prefab;
                PropBehaviour propBehaviour = pool.instantiate(prefab);
                liveObjects[key] = propBehaviour;

                propBehaviour.transform.SetParent(PiperMain.p.game.liveListObject.transform);

                bool isNewPlacement = !isInitialSync && distanceSquared < SystemSync.COUNT_AS_NEW_PLACEMENT_DISTANCE * SystemSync.COUNT_AS_NEW_PLACEMENT_DISTANCE;

                propBehaviour.transform.position = prop->position;
                propBehaviour.transform.eulerAngles = prop->eulerRotation;
                propBehaviour.propHandle = prop->getHandleToSelf();
                propBehaviour.resetVisualisation(isNewPlacement);
            }
        }
    }
}
