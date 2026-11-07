using System;
using UnityEngine;

namespace PipeWorld.Piper
{
    // Applies the building InputActions. Every edit loads the building, changes a clone, and writes the clone back only if it validates,
    // so the host, a joined client predicting its own action, and a replay all reach the same result.
    public static unsafe class BuildingActions
    {
        // The most recent rejection from an action this peer created, for the HUD.
        public static string lastRejection;
        public static float lastRejectionTime;

        public static void apply(World world, AllocatorContext allocator, InputAction action)
        {
            Func<uint> newId = () => EntityId.make(allocator.data.playerIndex, allocator.data.nextEntityId++);
            Func<uint> newPropId = () => EntityId.make(allocator.data.playerIndex, allocator.data.nextPropId++);

            switch (action.type)
            {
                case InputActionType.PlaceBuilding:
                    placeBuilding(world, newId, newPropId, action.data.placeBuilding);
                    break;
                case InputActionType.MoveBuildingEdge:
                {
                    MoveBuildingEdgeData d = action.data.moveBuildingEdge;
                    edit(world, d.building, newId, newPropId, m => m.moveEdge(d.edge, d.offset));
                    break;
                }
                case InputActionType.MoveBuildingVertex:
                {
                    MoveBuildingVertexData d = action.data.moveBuildingVertex;
                    edit(world, d.building, newId, newPropId, m => m.moveVertex(d.vertex, d.x, d.z));
                    break;
                }
                case InputActionType.ExtrudeBuildingEdge:
                {
                    ExtrudeBuildingEdgeData d = action.data.extrudeBuildingEdge;
                    edit(world, d.building, newId, newPropId, m => m.extrude(d.edge, d.offset));
                    break;
                }
                case InputActionType.CreateBuildingTri:
                {
                    CreateBuildingTriData d = action.data.createBuildingTri;
                    edit(world, d.building, newId, newPropId, m => m.createTri(d.edge, d.x, d.z));
                    break;
                }
                case InputActionType.SliceBuildingArea:
                {
                    SliceBuildingAreaData d = action.data.sliceBuildingArea;
                    edit(world, d.building, newId, newPropId, m => m.slice(d.area, d.x, d.z));
                    break;
                }
                case InputActionType.DeleteBuildingArea:
                {
                    DeleteBuildingAreaData d = action.data.deleteBuildingArea;
                    edit(world, d.building, newId, newPropId, m => m.deleteArea(d.area));
                    break;
                }
                case InputActionType.PlaceBuildingProp:
                {
                    PlaceBuildingPropData d = action.data.placeBuildingProp;
                    edit(world, d.building, newId, newPropId, m => m.placeProp(d.proto, d.face, d.faceRef, d.u, d.v));
                    break;
                }
                case InputActionType.SetBuildingHeights:
                {
                    SetBuildingHeightsData d = action.data.setBuildingHeights;
                    edit(world, d.building, newId, newPropId, m => m.setHeights(d.floorY, d.height));
                    break;
                }
                case InputActionType.DeleteBuildingObject:
                {
                    DeleteBuildingObjectData d = action.data.deleteBuildingObject;
                    edit(world, d.building, newId, newPropId, m => m.deleteObject(d.obj));
                    break;
                }
            }
        }

        private static void placeBuilding(World world, Func<uint> newId, Func<uint> newPropId, PlaceBuildingData d)
        {
            for (int i = 0; i < world.buildings.slabs.size; i++)
            {
                Building* other = world.buildings.slabs.get(i);
                if (other->slabHeader.allocated && Vector3.Distance(other->position, d.position) < BuildingConstants.MinBuildingSpacing)
                {
                    reject("Too close to another building.");
                    return;
                }
            }

            BuildingModel model = BuildingModel.createDefault(d.position, d.yaw, d.height, d.groundY, newId, newPropId);
            string problem = model.validate();
            if (problem != null)
            {
                reject(problem);
                return;
            }
            BuildingStore.save(world, model);
        }

        private static void edit(World world, uint buildingId, Func<uint> newId, Func<uint> newPropId, Func<BuildingModel, string> operation)
        {
            BuildingModel model = BuildingStore.load(world, buildingId, newId, newPropId);
            if (model == null)
                return;

            BuildingModel work = model.Clone();
            string error = operation(work);
            if (error != null)
            {
                reject(error);
                return;
            }

            BuildingStore.save(world, work);
        }

        private static void reject(string reason)
        {
            lastRejection = reason;
            lastRejectionTime = Time.realtimeSinceStartup;
            Debug.Log("[Piper] building edit rejected: " + reason);
        }
    }
}
