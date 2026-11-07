using System;
using UnityEngine;

namespace PipeWorld.Piper
{
    public enum InventoryItemPrototypeId : byte
    {
        None = 0,
        Obtainium = 1,
        LightUnit = 16,
        Rock = 34,
        Metal = 35,
        Flower = 36,
        Clay = 37,
        MAX_VALUE
    }

    public enum InventoryItemCategory : byte
    {
        SpecialBuilding,
        Building,
        UpgradeSlate,
        Junk,
    }

    public class InventoryItemPrototype
    {
        public InventoryItemPrototypeId id;
        public InventoryItemCategory category;
        public string name;
        public PropPrototypeId placedBuildingId = PropPrototypeId.None;
        public int stackMod = 1;
        public string description = "";
        public Color color = Color.white;
    }

    public class InventoryItemPrototypes
    {
        public static InventoryItemPrototypes instance;

        private readonly InventoryItemPrototype[] itemPrototypes;
        public InventoryItemPrototype this[InventoryItemPrototypeId id] => itemPrototypes[(int)id];

        public InventoryItemPrototypes()
        {
            InventoryItemPrototype[] temp =
            {
                new Builder(InventoryItemPrototypeId.Obtainium, InventoryItemCategory.SpecialBuilding, "Obtainium")
                    .description("The most useful material in the universe. Made of everything you can get.")
                    .color(new Color(0.93f, 0.75f, 0.30f))
                .build(),
                new Builder(InventoryItemPrototypeId.LightUnit, InventoryItemCategory.Building, "Light Unit")
                    .description("A placeable and self powered emergency lighting solution.")
                    .color(new Color(1.0f, 0.95f, 0.75f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Rock, InventoryItemCategory.Junk, "Rock")
                    .description("A good solid lump of stone.")
                    .color(new Color(0.58f, 0.57f, 0.55f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Metal, InventoryItemCategory.Junk, "Metal")
                    .description("Scrap metal, ready to be worked.")
                    .color(new Color(0.55f, 0.65f, 0.78f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Flower, InventoryItemCategory.Junk, "Flower")
                    .description("A wildflower, picked fresh.")
                    .color(new Color(0.95f, 0.55f, 0.72f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Clay, InventoryItemCategory.Junk, "Clay")
                    .description("Soft red clay from the riverbank.")
                    .color(new Color(0.78f, 0.45f, 0.32f))
                    .build(),
            };

            int maxEnum = (int)InventoryItemPrototypeId.MAX_VALUE;
            itemPrototypes = new InventoryItemPrototype[maxEnum];

            foreach (InventoryItemPrototype prototype in temp)
            {
                SUtil.ReleaseAssert(itemPrototypes[(int)prototype.id] == null, $"{prototype.id.ToString()} has a missing item prototype");
                itemPrototypes[(int)prototype.id] = prototype;
            }

            for (byte idNum = 0; idNum < maxEnum; idNum++)
            {
                if (!Enum.IsDefined(typeof(InventoryItemPrototypeId), idNum) || idNum == (int)InventoryItemPrototypeId.None)
                    continue;

                SUtil.ReleaseAssert(itemPrototypes[idNum] != null);
            }
        }

        private class Builder
        {
            private InventoryItemPrototype prototype = new InventoryItemPrototype();
            public Builder(InventoryItemPrototypeId id, InventoryItemCategory category, string name)
            {
                prototype.id = id;
                prototype.category = category;
                prototype.name = name;
            }

            public Builder stackMod(int stackMod)
            {
                prototype.stackMod = stackMod;
                return this;
            }

            public Builder description(string description) {
                prototype.description = description;
                return this;
            }

            public Builder color(Color color) {
                prototype.color = color;
                return this;
            }

            public InventoryItemPrototype build()
            {
                return prototype;
            }
        }
    }
}
