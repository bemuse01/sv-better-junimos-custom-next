namespace BetterJunimos {
    /// <summary>
    /// Constants used throughout the BetterJunimos mod
    /// </summary>
    internal static class ModDataKeys {
        // Mod unique ID prefix
        private const string ModPrefix = "bemuse01.BetterJunimosCustomNext";

        // Chest identification
        public const string JunimoChestMarker = "JunimoChest";

        // Save data keys
        public const string CropMapsSaveKey = ModPrefix + ".CropMaps";

        /// <summary>
        /// Gets the Junimo chest marker key with mod manifest unique ID
        /// </summary>
        public static string GetJunimoChestKey(string manifestUniqueId) => $"{manifestUniqueId}/{JunimoChestMarker}";
    }
}