using System;
using UnityEngine;

namespace PipeWorld.Piper
{
    public enum PropPrototypeId : byte
    {
        None = 0,
        PlaceableLamp = 45,
        DoorSingleItem = 29,
        CollectableGeneric = 57,
        RefreshableBox = 146,
        MAX_VALUE = 147,
    }

    public class PropPrototype
    {
        public PropPrototypeId id;
        public string name;
        public string description;
        public string descriptiveType = "Prop";

        public InventoryItemPrototypeId itemId;
        public int craftCost;
        public InventoryItemPrototypeId craftCostItemId = InventoryItemPrototypeId.Obtainium;

        public PropBehaviour prefab;
    }

    public class PropPrototypes
    {
        public static PropPrototypes instance = null;

        public PropPrototype[] prototypes { get; private set; }

        public PropPrototype this[PropPrototypeId id] => prototypes[(int)id];

        public static readonly PropPrototypeId[] allIds =
        {
            PropPrototypeId.CollectableGeneric,
            PropPrototypeId.DoorSingleItem,
            PropPrototypeId.PlaceableLamp,
            PropPrototypeId.RefreshableBox,
        };

        private class Builder
        {
            public PropPrototype prototype = new PropPrototype();

            public Builder(PropPrototypeId id)
            {
                prototype.id = id;
                prototype.itemId = InventoryItemPrototypeId.None;
            }

            public Builder name(string name) { prototype.name = name; return this; }
            public Builder description(string description) { prototype.description = description; return this; }
            public Builder descriptiveType(string descriptiveType) { prototype.descriptiveType = descriptiveType; return this; }
            public Builder costs(InventoryItemPrototypeId item, int cost) { prototype.craftCostItemId = item; prototype.craftCost = cost; return this; }

            public Builder item(InventoryItemPrototypeId itemId)
            {
                prototype.itemId = itemId;
                return this;
            }

            public PropPrototype build()
            {
                if (prototype.itemId != InventoryItemPrototypeId.None)
                    InventoryItemPrototypes.instance[prototype.itemId].placedBuildingId = prototype.id;

                if (prototype.name == null)
                    prototype.name = prototype.id.ToString();

                GameObject prefabTemp = Resources.Load<GameObject>("PropPrefabs/" + prototype.id.ToString());
                SUtil.ReleaseAssert(prefabTemp != null, $"couldn't find PropPrefabs/{prototype.id}");

                prototype.prefab = prefabTemp.GetComponent<PropBehaviour>();
                SUtil.ReleaseAssert(prototype.prefab != null, $"PropPrefabs/{prototype.id} is missing PropBehaviour");
                SUtil.ReleaseAssert(prototype.prefab.PropPrototypeId == prototype.id, $"PropPrefabs/{prototype.id} has incorrect PropPrototypeId");

                return prototype;
            }
        }

        public PropPrototypes()
        {
            PropPrototype[] temp =
            {
                new Builder(PropPrototypeId.CollectableGeneric)
                    .name("Salvage")
                    .descriptiveType("Collectable")
                    .description("Loose equipment that can be picked up.")
                    .build(),
                new Builder(PropPrototypeId.DoorSingleItem)
                    .name("Door")
                    .descriptiveType("Door")
                    .description("A door that opens and closes.")
                    .build(),
                new Builder(PropPrototypeId.PlaceableLamp)
                    .name("Lamp")
                    .descriptiveType("Light")
                    .description("A self-powered lamp with a switch.")
                    .item(InventoryItemPrototypeId.LightUnit)
                    .costs(InventoryItemPrototypeId.Obtainium, 10)
                    .build(),
                new Builder(PropPrototypeId.RefreshableBox)
                    .name("Supply Box")
                    .descriptiveType("Container")
                    .description("Slowly refills with obtainium. Hold to take it all.")
                    .build(),
            };

            prototypes = new PropPrototype[(int)PropPrototypeId.MAX_VALUE];
            foreach (PropPrototype prototype in temp)
            {
                SUtil.ReleaseAssert(prototypes[(int)prototype.id] == null, $"{prototype.id} has a duplicate prop prototype");
                prototypes[(int)prototype.id] = prototype;
            }

            foreach (PropPrototypeId id in allIds)
                SUtil.ReleaseAssert(prototypes[(int)id] != null, $"{id} has a missing prop prototype");
        }
    }

    public static class SystemInit
    {
        public static void init()
        {
            InventoryItemPrototypes.instance = new InventoryItemPrototypes();
            PropPrototypes.instance = new PropPrototypes();
        }
    }
}
