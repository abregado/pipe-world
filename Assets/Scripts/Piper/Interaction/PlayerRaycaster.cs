using System;
using System.Collections.Generic;
using PipeWorld.Player;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Casts the view ray once per game tick and keeps every hit, nearest first, so interaction modes and tooltips
    // read the same result.
    [RequireComponent(typeof(PlayerController))]
    public class PlayerRaycaster : MonoBehaviour
    {
        public const float RayLength = 100f;

        private static readonly Comparison<RaycastHit> byDistance = (a, b) => a.distance.CompareTo(b.distance);

        [NonSerialized] public RaycastHit[] raycastHits = new RaycastHit[64];
        [NonSerialized] public int raycastHitsCount;

        private RaycastHit[] hitsTemp = new RaycastHit[64];
        private PlayerController player;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        public Transform viewTransform => player.cameraPivot != null ? player.cameraPivot : transform;

        public void updateRaycast()
        {
            Transform view = viewTransform;
            Ray ray = new Ray(view.position, view.forward);

            int count = Physics.RaycastNonAlloc(ray, hitsTemp, RayLength, ~0, QueryTriggerInteraction.Ignore);
            while (count == hitsTemp.Length)
            {
                Array.Resize(ref hitsTemp, hitsTemp.Length * 2);
                count = Physics.RaycastNonAlloc(ray, hitsTemp, RayLength, ~0, QueryTriggerInteraction.Ignore);
            }

            Array.Sort(hitsTemp, 0, count, Comparer<RaycastHit>.Create(byDistance));

            if (raycastHits.Length != hitsTemp.Length)
                Array.Resize(ref raycastHits, hitsTemp.Length);

            raycastHitsCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (hitsTemp[i].collider.transform.IsChildOf(transform))
                    continue;

                raycastHits[raycastHitsCount++] = hitsTemp[i];
            }
        }
    }
}
