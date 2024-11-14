using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace WeightMod.src
{
    public static class Extensions
    {
        // Cette méthode s'assure que les attributs de l'objet ne sont pas nuls
        public static void EnsureAttributesNotNull(this CollectibleObject obj)
        {
            obj.Attributes ??= new JsonObject(new JObject());
        }

        // Cette méthode utilise un motif wildcard pour vérifier si un objet CollectibleObject correspond au motif
        public static bool WildCardMatchExt(this CollectibleObject obj, string location)
        {
            return obj.WildCardMatch(AssetLocation.Create(location));
        }
    }
}
