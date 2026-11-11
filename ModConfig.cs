using System.Collections.Generic;

namespace BetterJunimos {
    internal class ModConfig {
        public Dictionary<string, bool> JunimoAbilities { get; set; } = new() {
            ["HarvestCrops"] = true,
            ["PlantCrops"] = true,
            ["Fertilize"] = true,
            ["Water"] = true,
            ["ClearDeadCrops"] = true,
            ["HarvestBushes"] = true,
            ["HarvestForageCrops"] = true,
            ["VisitGreenhouse"] = true
        };

        public HutSettings JunimoHuts { get; set; } = new();

        internal class HutSettings {
            public int MaxJunimos { get; set; } = 3;
            public int MaxRadius { get; set; } = 7;
            public bool AvailableAfterCommunityCenterComplete { get; set; } = true;
            public bool AvailableImmediately { get; set; } = false;
            public bool ReducedCostToConstruct { get; set; } = true;
            public bool FreeToConstruct { get; set; } = false;
        }

        public JunimoImprovement JunimoImprovements { get; set; } = new();

        internal class JunimoImprovement {
            public bool CanWorkInRain { get; set; } = true;
            public bool CanWorkInWinter { get; set; } = true;
            public bool CanWorkInEvenings { get; set; } = true;
            public bool CanWorkInGreenhouse { get; set; } = true;
            public bool GiveExperience { get; set; } = false;
            public bool AvoidHarvestingFlowers { get; set; } = true;
            public bool AvoidHarvestingGiants { get; set; } = true;
            public bool HarvestEverythingOn28th { get; set; } = true;
            public bool AvoidPlantingCoffee { get; set; } = true;
            public bool AvoidPlantingOutOfSeason { get; set; } = true;
            public bool PlantMixedSeeds { get; set; } = false;
        }

        public FunSettings FunChanges { get; set; } = new();

        internal class FunSettings {
            public float RainyJunimoSpiritFactor = 0.65f;
            public bool JunimosAlwaysHaveLeafUmbrellas { get; set; } = false;
            public bool MoreColorfulLeafUmbrellas { get; set; } = false;
        }

        public OtherSettings Other { get; set; } = new();

        internal class OtherSettings {
            public bool ReceiveMessages { get; set; } = true;
        }
    }
}
