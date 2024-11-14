using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace WeightMod.src
{
    public static class Extensions
    {
        // Méthode pour s'assurer que les attributs de l'objet ne sont pas nuls
        public static void EnsureAttributesNotNull(this CollectibleObject obj)
        {
            obj.Attributes ??= new JsonObject(new JObject());
        }

        // Méthode d'extension pour la correspondance de motif avec wildcard
        public static bool WildCardMatchExt(this CollectibleObject obj, string location)
        {
            return obj.WildCardMatch(AssetLocation.Create(location));
        }
    }
}
