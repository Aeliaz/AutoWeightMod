using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using WeightMod.src;

namespace weightmod.src
{
    public class ItemCategorizer
    {
        public static string DetermineCategory(CollectibleObject item)
        {
            if (item == null) return "misc";

            // Check item type
            if (item.Tool != null) return "tool";
            if (item.Code.Path.Contains("weapon")) return "weapon";
            if (item.Code.Path.Contains("armor")) return "armor";
            
            // Check for food items by attributes
            if (item.Attributes != null && 
                (item.Code.Path.Contains("food") || 
                 item.Attributes["nutrition"].Exists)) return "food";
            
            // Check item attributes
            var attributes = item.Attributes;
            if (attributes != null)
            {
                // Check for specific keywords in the path
                string path = item.Code.Path.ToLower();
                
                if (path.Contains("ore")) return "ore";
                if (path.Contains("ingot") || path.Contains("metal")) return "metal";
                if (path.Contains("gem")) return "gem";
                
                // Check material attributes
                string material = attributes["material"]?.AsString()?.ToLower();
                if (!string.IsNullOrEmpty(material))
                {
                    if (material.Contains("wood")) return "wood";
                    if (material.Contains("stone")) return "stone";
                    if (material.Contains("metal")) return "metal";
                }
            }

            if (item is Block)
            {
                var block = item as Block;
                if (block.BlockMaterial == EnumBlockMaterial.Wood) return "woodblock";
                if (block.BlockMaterial == EnumBlockMaterial.Stone) return "stoneblock";
                if (block.BlockMaterial == EnumBlockMaterial.Metal) return "metalblock";
                if (block.BlockMaterial == EnumBlockMaterial.Glass) return "glassblock";
            }

            return "misc";
        }

        public static float CalculateBaseWeight(CollectibleObject item, string category, Config config)  // Modifié ici
        {
            float baseWeight = config.BASE_WEIGHTS_BY_CATEGORY.ContainsKey(category) ? config.BASE_WEIGHTS_BY_CATEGORY[category] : 200f;
            
            // Apply material multiplier if available
            if (item.Attributes != null)
            {
                string material = item.Attributes["material"]?.AsString()?.ToLower();
                if (!string.IsNullOrEmpty(material))
                {
                    foreach (var multiplier in config.MATERIAL_MULTIPLIERS)
                    {
                        if (material.Contains(multiplier.Key))
                        {
                            baseWeight *= multiplier.Value;
                            break;
                        }
                    }
                }
            }

            // Adjust by stack size
            if (item.MaxStackSize > 1)
            {
                baseWeight *= 0.8f; // Stackable items are generally smaller
            }

            return baseWeight;
        }
    }
}