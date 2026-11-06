using System.Collections.Generic;
using UnityEngine;

namespace PipeWorld.Piper
{
    public unsafe class OtherPlayerSyncer
    {
        private readonly Dictionary<NetUid, OtherPlayerBehaviour> avatars = new Dictionary<NetUid, OtherPlayerBehaviour>();
        private readonly List<NetUid> staleScratch = new List<NetUid>();
        private readonly OtherPlayerBehaviour prefab;
        private readonly Transform parent;

        public OtherPlayerSyncer(Transform parent)
        {
            this.parent = parent;
            prefab = Resources.Load<OtherPlayerBehaviour>("OtherPlayer");
            SUtil.ReleaseAssert(prefab != null, "Resources/OtherPlayer.prefab is missing");
        }

        public int avatarCount => avatars.Count;

        public void sync(World world, NetworkManager networkManager, NetUid localPlayerId)
        {
            staleScratch.Clear();
            foreach (NetUid id in avatars.Keys)
                staleScratch.Add(id);

            for (int i = 0; i < Constants.MAX_PLAYER_COUNT; i++)
            {
                PlayerData player = world.data.players.get(i);
                NetUid id = player.id;
                if (id.Equals(NetUid.Empty) || id.Equals(localPlayerId) || id.Equals(Constants.spectatorGuid))
                    continue;
                if (!networkManager.isPlayerOnline(id))
                    continue;

                staleScratch.Remove(id);

                if (!avatars.TryGetValue(id, out OtherPlayerBehaviour avatar))
                {
                    avatar = Object.Instantiate(prefab, parent);
                    avatar.name = "Player " + player.getName();
                    avatars[id] = avatar;
                }

                avatar.SetTarget(player.position, player.rotation, player.isCrouching, player.getName());
            }

            foreach (NetUid id in staleScratch)
            {
                Object.Destroy(avatars[id].gameObject);
                avatars.Remove(id);
            }
        }

        public void destroyAll()
        {
            foreach (OtherPlayerBehaviour avatar in avatars.Values)
                Object.Destroy(avatar.gameObject);
            avatars.Clear();
        }
    }
}
