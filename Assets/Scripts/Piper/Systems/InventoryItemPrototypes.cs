using System;
using UnityEngine;

namespace PipeWorld.Piper
{
    public enum InventoryItemPrototypeId : byte
    {
        None = 0,
        Obtainium = 1,
        Platform = 2,
        GreenSlate = 3,
        Matrix = 4,
        Lance = 11,
        DisintegratorCharge = 12,
        PowerCell = 13,
        OverrideModule = 14,
        HackSpike = 15,
        LightUnit = 16,
        PlasmaCell = 17,
        Substrate = 19,
        Socket = 24,
        Kit = 25,
        Cluster = 26,
        Tank = 27,
        Board = 28,
        Lattice = 29,
        Slate = 30,
        Shard = 31,
        Illuminator = 32,
        Orb = 33,
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
                new Builder(InventoryItemPrototypeId.Platform, InventoryItemCategory.SpecialBuilding, "Platform")
                    .color(new Color(0.65f, 0.70f, 0.75f))
                    .build(),
                new Builder(InventoryItemPrototypeId.LightUnit, InventoryItemCategory.Building, "Light Unit")
                    .description("A placeable and self powered emergency lighting solution.")
                    .color(new Color(1.0f, 0.95f, 0.75f))
                    .build(),
                new Builder(InventoryItemPrototypeId.GreenSlate, InventoryItemCategory.UpgradeSlate, "Green upgrade slate")
                    .color(new Color(0.45f, 0.85f, 0.45f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Tank, InventoryItemCategory.Junk, "Tank")
                    .description("Its filled with something.")
                    .color(new Color(0.55f, 0.40f, 0.25f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Cluster, InventoryItemCategory.Junk, "Cluster")
                    .description("Generic equipment that can be recycled.")
                    .color(new Color(0.60f, 0.60f, 0.60f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Board, InventoryItemCategory.Junk, "Board")
                    .description("A flat mass of electronics.")
                    .color(new Color(0.30f, 0.60f, 0.50f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Socket, InventoryItemCategory.UpgradeSlate, "Fluid Socket")
                    .description("An upgrade for various machines to output whichever fluids are inside. Made in the Socket Station.")
                    .color(new Color(0.35f, 0.75f, 0.85f))
                    .build(),
                new Builder(InventoryItemPrototypeId.OverrideModule, InventoryItemCategory.UpgradeSlate, "Override Module")
                    .description("A hacking tool for opening high security things.")
                    .color(new Color(0.65f, 0.35f, 0.85f))
                .build(),
                new Builder(InventoryItemPrototypeId.HackSpike, InventoryItemCategory.UpgradeSlate, "Hack Spike")
                    .description("A simple hacking tool for opening things. Made in the Keysmith.")
                    .color(new Color(0.85f, 0.30f, 0.55f))
                .build(),
                new Builder(InventoryItemPrototypeId.PowerCell, InventoryItemCategory.UpgradeSlate, "Power Cell")
                    .description("Used to upgrade machines.")
                    .color(new Color(0.95f, 0.85f, 0.25f))
                .build(),
                new Builder(InventoryItemPrototypeId.Lance, InventoryItemCategory.Junk, "Lance")
                    .description("A hardened FixFoam-tipped tool. Made in the Crimper.")
                    .color(new Color(0.80f, 0.45f, 0.30f))
                    .build(),
                new Builder(InventoryItemPrototypeId.DisintegratorCharge, InventoryItemCategory.SpecialBuilding, "Disintegrator Charge")
                    .description("Packaged nanites which can recycle hardened objects.")
                    .color(new Color(0.90f, 0.35f, 0.20f))
                .build(),
                new Builder(InventoryItemPrototypeId.PlasmaCell, InventoryItemCategory.UpgradeSlate, "Plasma Cell")
                    .color(new Color(0.95f, 0.40f, 0.80f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Substrate, InventoryItemCategory.UpgradeSlate, "Substrate")
                    .description("A crystallised tech substrate.")
                    .color(new Color(0.35f, 0.65f, 0.95f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Kit, InventoryItemCategory.SpecialBuilding, "Machine Kit")
                    .description("Used to build a single machine. Made in the Fabricator")
                    .color(new Color(0.90f, 0.60f, 0.25f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Lattice, InventoryItemCategory.Junk, "Lattice")
                    .description("A compressed framework of machine parts. Made in the Compressor.")
                    .color(new Color(0.50f, 0.70f, 0.90f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Slate, InventoryItemCategory.Junk, "Slate")
                    .description("A dense sublimated wafer. Made in the Sublimator.")
                    .color(new Color(0.70f, 0.75f, 0.80f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Shard, InventoryItemCategory.Junk, "Shard")
                    .description("A sharp fragment of hardened material.")
                    .color(new Color(0.85f, 0.85f, 0.55f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Illuminator, InventoryItemCategory.Junk, "Illuminator")
                    .description("A self-contained light emitter salvaged from equipment.")
                    .color(new Color(0.95f, 0.90f, 0.55f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Orb, InventoryItemCategory.Junk, "Orb")
                    .description("A sealed spherical core of unknown function.")
                    .color(new Color(0.75f, 0.60f, 0.95f))
                    .build(),
                new Builder(InventoryItemPrototypeId.Matrix, InventoryItemCategory.Junk, "Matrix")
                    .description("A lattice grown around a Tech Substrate. Made in the Collector.")
                    .color(new Color(0.45f, 0.95f, 0.75f))
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
