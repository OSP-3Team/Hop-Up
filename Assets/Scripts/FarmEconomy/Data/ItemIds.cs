namespace HopUp.FarmEconomy
{
    /// <summary>아이템 ID 규칙. 발판 scaffold_{단계}, 씨앗 seed_{작물}, 수확한 당근 carrot_{작물}, 음식 food_{레시피}.</summary>
    public static class ItemIds
    {
        public const string ScaffoldPrefix = "scaffold_";
        public const string SeedPrefix = "seed_";
        public const string CarrotPrefix = "carrot_";

        public static string Scaffold(int tier) { return ScaffoldPrefix + tier; }
        public static string Seed(string cropId) { return SeedPrefix + cropId; }
        public static string Carrot(string cropId) { return CarrotPrefix + cropId; }

        /// <summary>씨앗 아이템이면 작물 ID, 아니면 null.</summary>
        public static string CropIdFromSeed(string itemId) { return Strip(itemId, SeedPrefix); }

        /// <summary>당근 아이템이면 작물 ID, 아니면 null.</summary>
        public static string CropIdFromCarrot(string itemId) { return Strip(itemId, CarrotPrefix); }

        static string Strip(string itemId, string prefix)
        {
            if (string.IsNullOrEmpty(itemId) || !itemId.StartsWith(prefix, System.StringComparison.Ordinal)) return null;
            string rest = itemId.Substring(prefix.Length);
            return rest.Length > 0 ? rest : null;
        }
    }
}
