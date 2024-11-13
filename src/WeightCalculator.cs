using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace WeightMod.src
{
    public class WeightCalculator
    {
        private readonly Config weightModConfig;
        private readonly Dictionary<string, float> materialDensities = new Dictionary<string, float>
        {
            // Métaux (g/cm³)
            {"copper", 8.96f},
            {"iron", 7.874f},
            {"gold", 19.32f},
            {"silver", 10.49f},
            {"tin", 7.365f},
            {"lead", 11.34f},
            {"zinc", 7.14f},
            {"brass", 8.73f},
            {"bronze", 8.77f},
            {"steel", 7.85f}
        };

        public WeightCalculator(Config config)
        {
            this.weightModConfig = config;
        }

        public float CalculateItemWeight(Item item)
        {
            if (item == null || item.Code == null) return 0f;

            string itemCode = item.Code.ToString();
            
            // Vérifier d'abord dans la configuration existante de WeightMod
            if (weightModConfig.WEIGHTS_FOR_ITEMS.TryGetValue(itemCode, out var weightInfo) && weightInfo.Weight.HasValue)
            {
                return weightInfo.Weight.Value;
            }

            // Vérifier les bonus items
            if (weightModConfig.WEIGHTS_BONUS_ITEMS.TryGetValue(itemCode, out var bonusWeight))
            {
                return bonusWeight;
            }

            // Si l'item n'est pas dans la config, calculer son poids automatiquement
            string itemCodeLower = item.Code.Path.ToLower();
            float baseWeight = GetBaseWeight(itemCodeLower);
            float materialMultiplier = GetMaterialMultiplier(itemCodeLower);

            float calculatedWeight = baseWeight * materialMultiplier;

            // Ajuster en fonction de la stack size
            if (item.MaxStackSize > 1)
            {
                calculatedWeight = Math.Max(1f, calculatedWeight / Math.Max(1, item.MaxStackSize / 4f));
            }

            // Si un poids a été calculé, l'ajouter à la configuration
            if (calculatedWeight > 0)
            {
                weightModConfig.WEIGHTS_FOR_ITEMS[itemCode] = new ItemWeightInfo
                {
                    Weight = calculatedWeight,
                    Category = "auto-calculated"
                };
            }

            return calculatedWeight;
        }

        public float CalculateBlockWeight(Block block)
        {
            if (block == null || block.Code == null) return 1000f;

            string blockCode = block.Code.ToString();
            
            // Vérifier les blocs configurés
            if (weightModConfig.WEIGHTS_FOR_BLOCKS.TryGetValue(blockCode, out var weightInfo) && weightInfo.Weight.HasValue)
            {
                return weightInfo.Weight.Value;
            }

            string blockCodeLower = block.Code.Path.ToLower();
            float baseWeight = 1000f;
            float materialMultiplier = GetMaterialMultiplier(blockCodeLower);

            // Ajustements spéciaux
            if (blockCodeLower.Contains("stairs") || blockCodeLower.Contains("slab"))
            {
                materialMultiplier *= 0.5f;
            }
            else if (blockCodeLower.Contains("wall"))
            {
                materialMultiplier *= 0.8f;
            }
            else if (blockCodeLower.Contains("fence") || blockCodeLower.Contains("gate"))
            {
                materialMultiplier *= 0.3f;
            }

            float calculatedWeight = baseWeight * materialMultiplier;

            // Ajouter le nouveau bloc à la configuration
            if (calculatedWeight > 0 && calculatedWeight != 1000f)
            {
                weightModConfig.WEIGHTS_FOR_BLOCKS[blockCode] = new ItemWeightInfo
                {
                    Weight = calculatedWeight,
                    Category = "auto-calculated"
                };
            }

            return calculatedWeight;
        }

        private float GetBaseWeight(string itemCode)
        {
            if (itemCode.Contains("pickaxe")) return 1200f;
            if (itemCode.Contains("axe")) return 1000f;
            if (itemCode.Contains("shovel")) return 800f;
            if (itemCode.Contains("sword")) return 1500f;
            if (itemCode.Contains("spear")) return 1200f;
            if (itemCode.Contains("bow")) return 800f;
            if (itemCode.Contains("arrow")) return 60f;
            if (itemCode.Contains("hammer")) return 1000f;
            if (itemCode.Contains("chisel")) return 200f;
            
            // Armures
            if (itemCode.Contains("helmet")) return 2000f;
            if (itemCode.Contains("chestplate")) return 4000f;
            if (itemCode.Contains("leggings")) return 3000f;
            if (itemCode.Contains("boots")) return 1500f;
            
            // Conteneurs
            if (itemCode.Contains("chest")) return 5000f;
            if (itemCode.Contains("barrel")) return 4000f;
            if (itemCode.Contains("pot")) return 1500f;
            
            return 100f;
        }

        private float GetMaterialMultiplier(string itemCode)
        {
            foreach (var material in materialDensities)
            {
                if (itemCode.Contains(material.Key))
                {
                    return material.Value;
                }
            }
            return 1f;
        }
    }
}
