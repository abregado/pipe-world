using UnityEngine;

namespace PipeWorld.Piper
{
    // Avatar for a remote player. Positions arrive roughly 30 times a second, so the view eases toward the latest one.
    public class OtherPlayerBehaviour : MonoBehaviour
    {
        public Transform head;
        public Transform body;
        public TextMesh nameLabel;

        private const float FollowRate = 18f;
        private const float StandingBodyHeight = 1f;
        private const float CrouchBodyHeight = 0.6f;

        private Vector3 targetPosition;
        private float targetYaw;
        private float targetPitch;
        private bool targetCrouching;
        private bool hasTarget;

        public void SetTarget(Vector3 position, Vector3 eulerRotation, bool crouching, string playerName)
        {
            targetPosition = position;
            targetYaw = eulerRotation.y;
            targetPitch = eulerRotation.x > 180f ? eulerRotation.x - 360f : eulerRotation.x;
            targetCrouching = crouching;
            nameLabel.text = playerName;

            if (!hasTarget)
            {
                transform.position = position;
                hasTarget = true;
            }
        }

        private void Update()
        {
            if (!hasTarget)
                return;

            float t = 1f - Mathf.Exp(-FollowRate * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, targetYaw, 0f), t);
            head.localRotation = Quaternion.Euler(targetPitch, 0f, 0f);

            float bodyHeight = targetCrouching ? CrouchBodyHeight : StandingBodyHeight;
            body.localScale = new Vector3(1f, Mathf.Lerp(body.localScale.y, bodyHeight, t), 1f);
            head.localPosition = new Vector3(0f, body.localScale.y + 0.4f, 0f);

            Camera view = Camera.main;
            if (view != null)
            {
                Transform label = nameLabel.transform;
                label.position = head.position + Vector3.up * 0.5f;
                label.rotation = Quaternion.LookRotation(label.position - view.transform.position);
            }
        }
    }
}
