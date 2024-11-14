using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Datastructures;

namespace WeightMod.src
{
    public class ItemWeightInfo
    {
        public float? Weight { get; set; }
        public string Category { get; set; }
    }

    public class Config
    {
        public float MAX_PLAYER_WEIGHT { get; set; } = 20000;

        public float WEIGH_PLAYER_THRESHOLD { get; set; } = 0.7f;

        public float RATIO_MIN_MAX_WEIGHT_PLAYER_HEALTH { get; set; } = 0.6f;

        public float ACCUM_TIME_WEIGHT_CHECK { get; set; } = 2f;

        public bool PERCENT_MODIFIER_USED_ON_RAW_WEIGHT { get; set; } = false;

        public string CLASS_WEIGHT_BONUS { get; set; } = "commoner:0;hunter:500;malefactor:-500;clockmaker:-1000;blackguard:2000;tailor:-2000";

        public float HOW_OFTEN_RECHECK { get; set; } = 10f;

        public string INFO_COLOR_WEIGHT { get; set; } = "#F0C20B";
        public string INFO_COLOR_WEIGHT_BONUS { get; set; } = "#1F920E";

        public OrderedDictionary<string, ItemWeightInfo> WEIGHTS_FOR_ITEMS { get; set; } = new OrderedDictionary<string, ItemWeightInfo>();

        public OrderedDictionary<string, ItemWeightInfo> WEIGHTS_FOR_BLOCKS { get; set; } = new OrderedDictionary<string, ItemWeightInfo>();

        public Dictionary<string, int> WEIGHTS_FOR_ENDS_WITH { get; set; } = new Dictionary<string, int>
        {
            { "game:ore-poor", 170}
        };

        public Dictionary<string, int> WEIGHTS_BONUS_ITEMS { get; set; } = new Dictionary<string, int>
        {
            { "game:basket", 1000},
            { "game:backpack", 2000},
            { "game:linensack", 1300},
            { "game:miningbag", 3300}
        };

        public Dictionary<string, float> BASE_WEIGHTS_BY_CATEGORY { get; set; } = new Dictionary<string, float>
        {
            {"tool", 500f},
            {"weapon", 800f},
            {"armor", 1000f},
            {"ore", 1000f},
            {"metal", 800f},
            {"wood", 400f},
            {"stone", 1000f},
            {"gem", 200f},
            {"woodblock", 400f},
            {"stoneblock", 1000f},
            {"metalblock", 1200f},
            {"glassblock", 600f},
            {"food", 100f},
            {"craftingmaterial", 200f},
            {"clothing", 300f},
            {"misc", 200f}
        };

        public Dictionary<string, float> MATERIAL_MULTIPLIERS { get; set; } = new Dictionary<string, float>
        {
            {"wood", 0.8f},
            {"stone", 1.2f},
            {"metal", 1.5f},
            {"cloth", 0.3f},
            {"leather", 0.5f},
            {"glass", 0.7f},
            {"gem", 1.0f}
        };
        public Dictionary<string, float> classBonuses { get; set; } = new Dictionary<string, float>();
        public string HUD_POSITION { get; set; } = "saturationstatbar";
        public string UNKNOWN_CATEGORY { get; set; } = "Unknown";

        // Méthode GetItemWeight ajoutée ici
        public ItemWeightInfo GetItemWeight(string itemCode)
        {
            // Vérifie si l'item a un poids spécifique défini dans WEIGHTS_FOR_ITEMS
            if (WEIGHTS_FOR_ITEMS.TryGetValue(itemCode, out var itemWeightInfo))
            {
                return itemWeightInfo;
            }

            // Sinon, vérifie si l'item appartient à une catégorie de poids de BASE_WEIGHTS_BY_CATEGORY
            foreach (var category in BASE_WEIGHTS_BY_CATEGORY)
            {
                if (itemCode.Contains(category.Key))
                {
                    return new ItemWeightInfo { Weight = category.Value, Category = category.Key };
                }
            }

            // Si aucune correspondance, retourne un ItemWeightInfo indiquant une catégorie inconnue
            return new ItemWeightInfo { Category = "Unknown" };
        }
    }
}
