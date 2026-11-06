using UnityEngine;

namespace PipeWorld.Piper
{
    public class SingleplayerNetworkManager : NetworkManager
    {
        public SingleplayerNetworkManager()
        {
            Debug.Log("starting singleplayer network manager");
        }

        public override void update(World world, InputActionManager inputActionManager)
        {
            PlayerData myPlayerData = PiperMain.p.game.buildLocalPlayerData();
            if (!updatePlayerData(world, myPlayerData))
                world.data.players.set(0, myPlayerData);
            loadAllocatorStateIfNeeded(world);

            InputActionManager.apply(world, inputActionManager.myInputActions);
            inputActionManager.myInputActions.Clear();
            world.update();
        }

        public override bool isPlayerOnline(NetUid playerId)
        {
            return playerId == PiperMain.p.game.playerId;
        }

        public override void Dispose() { }
    }
}
