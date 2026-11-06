using ImGuiNET;
using UnityEngine;

namespace PipeWorld.Piper
{
    // The Piper section of the Dear ImGui debug window. Every control queues an InputAction, so it behaves the same hosting, joined or alone.
    public static unsafe class PiperDebug
    {
        private const float PlaceDistance = 3f;

        public static void Draw()
        {
            PiperGame game = PiperMain.p != null ? PiperMain.p.game : null;
            if (game == null || game.playerObject == null)
                return;

            ImGui.Separator();
            ImGui.TextUnformatted($"Piper: {Mode(game)}, tick {game.world.data.currentTick}, {game.ActivePlayerCount()} player(s)");

            if (ImGui.Button("+10 Obtainium"))
                game.inputActionManager.add(new AdjustInventoryData { item = InventoryItemPrototypeId.Obtainium, amount = 10 });
            ImGui.SameLine();
            if (ImGui.Button("+3 Light Unit"))
                game.inputActionManager.add(new AdjustInventoryData { item = InventoryItemPrototypeId.LightUnit, amount = 3 });

            DrawPlaceButton(game, "Place lamp (uses a Light Unit)", PropPrototypeId.PlaceableLamp, useItem: true);
            DrawPlaceButton(game, "Place salvage", PropPrototypeId.CollectableGeneric, useItem: false);
            DrawPlaceButton(game, "Place door", PropPrototypeId.DoorSingleItem, useItem: false);
            DrawPlaceButton(game, "Place supply box", PropPrototypeId.RefreshableBox, useItem: false);

            Prop* target = TargetedProp(game);
            ImGui.BeginDisabled(target == null);
            if (ImGui.Button("Delete targeted prop") && target != null)
                game.inputActionManager.add(new DeletePropData { prop = new PropNetworkIdentifier(target) });
            ImGui.SameLine();
            if (ImGui.Button("Nudge targeted prop 1 m up") && target != null)
                game.inputActionManager.add(new MovePropData { prop = new PropNetworkIdentifier(target), newPosition = target->position + new Vec3f(0f, 1f, 0f) });
            ImGui.EndDisabled();
        }

        private static string Mode(PiperGame game)
        {
            if (game.IsHostingMP()) return "hosting";
            if (game.IsClientInMP()) return "client";
            return "singleplayer";
        }

        private static void DrawPlaceButton(PiperGame game, string label, PropPrototypeId id, bool useItem)
        {
            if (!ImGui.Button(label))
                return;

            Transform view = game.raycaster.viewTransform;
            Vector3 flatForward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 position = game.playerObject.transform.position + flatForward * PlaceDistance;
            if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out RaycastHit ground, 100f, ~0, QueryTriggerInteraction.Ignore))
                position = ground.point;

            // Whole-centimetre positions keep PropNetworkIdentifier lookups exact when a position round-trips through a save.
            position = new Vector3(Mathf.Round(position.x * 100f) / 100f, Mathf.Round(position.y * 100f) / 100f, Mathf.Round(position.z * 100f) / 100f);
            Vector3 rotation = new Vector3(0f, view.eulerAngles.y + 180f, 0f);

            if (useItem)
                game.inputActionManager.add(new PlacePropUsingItemData { propPrototypeId = id, position = position, eulerRotation = rotation });
            else
                game.inputActionManager.add(new PlacePropData { propPrototypeId = id, position = position, eulerRotation = rotation });
        }

        private static Prop* TargetedProp(PiperGame game)
        {
            PlayerRaycaster raycaster = game.raycaster;
            if (raycaster.raycastHitsCount == 0)
                return null;

            PropBehaviour behaviour = raycaster.raycastHits[0].collider.GetComponentInParent<PropBehaviour>();
            return behaviour == null ? null : behaviour.propHandle.tryGetPointer();
        }
    }
}
