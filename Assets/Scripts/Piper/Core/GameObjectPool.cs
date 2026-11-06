using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    public class GameObjectPool<T> where T : MonoBehaviour
    {
        private List<T> freeList = new List<T>();

        public T instantiate(T prefab, Transform parent = null)
        {
            T liveObject = null;
            if (freeList.Count > 0)
            {
                liveObject = freeList[freeList.Count - 1];
                freeList.RemoveAt(freeList.Count - 1);
                liveObject.gameObject.SetActive(true);
            }
            else
            {
                liveObject = GameObject.Instantiate(prefab);
            }

            liveObject.transform.SetParent(parent);
            return liveObject;
        }

        public void destroyAll()
        {
            foreach (T free in freeList)
                UnityEngine.Object.Destroy(free.gameObject);
            freeList.Clear();
        }

        public void destroy(T obj)
        {
            obj.gameObject.SetActive(false);
            freeList.Add(obj);

            obj.transform.parent = PiperMain.p.game.freeListObject.transform;
        }
    }
}
