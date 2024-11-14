using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Config = WeightMod.src.Config;
using WeightMod.src;

namespace WeightMod.src
{
    public class WeightMod : ModSystem
    {
        internal static ICoreServerAPI sapi;
        internal static ICoreClientAPI capi;
        private static Dictionary<string, float> mapLastCalculatedPlayerWeight = new Dictionary<string, float>();
        private static Dictionary<string, bool> inventoryWasModified = new Dictionary<string, bool>();
        private static Dictionary<string, float> classBonuses = new Dictionary<string, float>();
        internal static Dictionary<int, float> itemIdToWeight = new Dictionary<int, float>();
        internal static Dictionary<int, float> blockIdToWeight = new Dictionary<int, float>();
        internal static Dictionary<int, float> itemBonusIdToWeight = new Dictionary<int, float>();
        private static string bArrIITW;
        private static string bArrBITW;
        private static string bArrIBITW;
        private static Harmony harmonyInstance;
        internal static IServerNetworkChannel serverChannel;
        internal static IClientNetworkChannel clientChannel;
        private const string harmonyID = "WeightMod.Patches";
        private static WeightMod instance;
        public static Config Config { get; private set; } = null!;
        private ItemCategorizer categorizer;

        public WeightMod()
        {
            instance = this;
        }

        private void ProcessBlockWeight(Block block)
        {
            if (block?.Code == null) return;

            // S'assure que les attributs du bloc ne sont pas nuls
            block.EnsureAttributesNotNull();

            string blockCode = block.Code.ToString();

            // Utiliser un code générique pour le type de bloc en retirant le suffixe de la variante
            string genericCode = GetGenericBlockCode(blockCode);
        
            // Si le modèle générique n'existe pas encore dans la configuration, on l'ajoute
            if (!Config.WEIGHTS_FOR_BLOCKS.ContainsKey(genericCode))
            {
                var categorizer = new ItemCategorizer(sapi, Config);
                string category = ItemCategorizer.DetermineCategory(block);
                var baseWeight = categorizer.CalculateBaseWeight(block, category);
        
                // Ajouter le poids pour le modèle générique
                Config.WEIGHTS_FOR_BLOCKS[genericCode] = new ItemWeightInfo
                {
                    Weight = baseWeight,
                    Category = category
                };
        
                sapi.Logger.Debug($"Added block weight for generic code: {genericCode} = {baseWeight}");
            }
            else
            {
                sapi.Logger.Debug($"Skipped duplicate variant: {blockCode} (using generic code {genericCode})");
            }
        }
        
        // Fonction auxiliaire pour obtenir le code générique en retirant le suffixe
        private string GetGenericBlockCode(string blockCode)
        {
            // Sépare le code en segments en utilisant des tirets comme délimiteurs
            var segments = blockCode.Split('-').ToList();

            // Liste des valeurs typiques détectées comme variantes dans les segments
            HashSet<string> variablePatterns = new HashSet<string>
            {
                "north", "south", "east", "west", "n", "s", "e", "w",
                "ne", "nw", "se", "sw", "closed", "opened", "poor", "medium", "rich", 
                "bountiful", "1", "2", "3", "4", "5", "6", "n1", "n2", "0", "p1", "p2"
            };
        
            // Parcourt chaque segment et remplace les segments "variables" par un '*'
            for (int i = 0; i < segments.Count; i++)
            {
                string segment = segments[i];
        
                // Remplace le segment s'il est un motif variable ou un chiffre unique
                if (variablePatterns.Contains(segment) || int.TryParse(segment, out _))
                {
                    segments[i] = "*";
                }
            }
        
            // Reconstruit le code avec les segments restants, en remplaçant les segments variables par '-*'
            string genericCode = string.Join("-", segments);
            
            // Supprime les éventuels doubles '–*' consécutifs en cas de succession de segments variables
            return genericCode.Replace("-*", "*").Replace("*-", "*");
        }

        private void ProcessItemWeight(Item item)
        {
            if (item?.Code == null) return;

            // S'assure que les attributs de l'item ne sont pas nuls
            item.EnsureAttributesNotNull();
        
            string itemCode = item.Code.ToString();

            // Utiliser un code générique pour le type d'item en retirant le suffixe de la variante
            string genericCode = GetGenericItemCode(itemCode);

            // Si le modèle générique n'existe pas encore dans la configuration, on l'ajoute
            if (!Config.WEIGHTS_FOR_ITEMS.ContainsKey(genericCode))
            {
                var categorizer = new ItemCategorizer(sapi, Config);
                string category = ItemCategorizer.DetermineCategory(item);
                var baseWeight = categorizer.CalculateBaseWeight(item, category);
        
                // Ajouter le poids pour le modèle générique
                Config.WEIGHTS_FOR_ITEMS[genericCode] = new ItemWeightInfo
                {
                    Weight = baseWeight,
                    Category = category
                };
        
                sapi.Logger.Debug($"Added item weight for generic code: {genericCode} = {baseWeight}");
            }
            else
            {
                sapi.Logger.Debug($"Skipped duplicate variant: {itemCode} (using generic code {genericCode})");
            }
        }
        
        // Fonction auxiliaire pour obtenir le code générique en retirant les segments variables dans le nom de l'item
        private string GetGenericItemCode(string itemCode)
        {
            // Divise l'itemCode en segments séparés par des tirets
            var segments = itemCode.Split('-');
    
            // Cherche les parties communes pour chaque segment entre items avec le même préfixe
            var genericSegments = new List<string>();
        
            foreach (var segment in segments)
            {
                // Filtre les segments contenant uniquement des chiffres ou certains mots courants indiquant une variation
                if (int.TryParse(segment, out _) || segment == "north" || segment == "south" || segment == "east" ||
                    segment == "west" || segment == "top" || segment == "bottom" || segment == "lower" || segment == "upper")
                {
                    genericSegments.Add("*");
                   break; // Ne pas ajouter les segments suivants après la première partie variable détectée
                }
                else
                {
                    genericSegments.Add(segment);
                }
            }
        
            // Recombine les segments avec des tirets
            return string.Join("-", genericSegments);
        }
        
        public void OnPlayerNowPlaying(IServerPlayer byPlayer)
        {
            var ep = new EntityBehaviorWeightable(byPlayer.Entity);
            ep.PostInit();
            byPlayer.Entity.AddBehavior(ep);
        }

        public void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            if (byPlayer.Entity.HasBehavior<EntityBehaviorWeightable>())
            {
                byPlayer.Entity.RemoveBehavior(byPlayer.Entity.GetBehavior<EntityBehaviorWeightable>());
            }
        }
        public static Dictionary<string, float> GetClassBonuses()
        {
            return classBonuses;
        }

        public static Dictionary<string, float> GetLastCalculatedPlayerWeight()
        {
            return mapLastCalculatedPlayerWeight;
        }

        public static Dictionary<string, bool> GetInventoryWasModified()
        {
            return inventoryWasModified;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            classBonuses = new Dictionary<string, float>();
            api.RegisterEntityBehaviorClass("affectedByItemsWeight", typeof(EntityBehaviorWeightable));
            
            harmonyInstance = new Harmony(harmonyID);
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityInAir).GetMethod("Applicable"), 
                prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_ApplicableInAir")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityInLiquid).GetMethod("Applicable"), 
                prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_ApplicableInLiquid")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityOnGround).GetMethod("Applicable"), 
                prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_ApplicableOnGround")));
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            base.StartClientSide(api);

            clientChannel = capi.Network.RegisterChannel("weightmod")
                .RegisterMessageType(typeof(syncWeightPacket))
                .SetMessageHandler<syncWeightPacket>(OnReceiveWeightData);
        
            sapi.Logger.Notification("Client-side network channel 'weightmod' registered.");
        }

        // Gestionnaire pour le message reçu
        private void OnReceiveWeightData(syncWeightPacket packet)
        {
            // Log pour confirmer la réception des données
            capi.Logger.Debug("Received weight data packet from server.");
            // Vous pouvez traiter les données ici
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);

            mapLastCalculatedPlayerWeight = new Dictionary<string, float>();
            inventoryWasModified = new Dictionary<string, bool>();
            itemIdToWeight = new Dictionary<int, float>();
            blockIdToWeight = new Dictionary<int, float>();
            itemBonusIdToWeight = new Dictionary<int, float>();

            LoadConfig();
            categorizer = new ItemCategorizer(api, Config);

            LoadClassBonusesMap();
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnServerExit);
            api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, FillWeightDictionary);

            serverChannel = sapi.Network.RegisterChannel("weightmod");
            serverChannel.RegisterMessageType(typeof(syncWeightPacket));
            api.Event.PlayerNowPlaying += SendNewValues;

            harmonyInstance = new Harmony(harmonyID);
            harmonyInstance.Patch(typeof(Vintagestory.API.Common.InventoryBase).GetMethod("DidModifyItemSlot"), 
                postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_OnItemSlotModified")));
        }

        private void LoadWeightsFromExternalConfig(string jsonPath)
        {
            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                dynamic configData = JsonConvert.DeserializeObject(jsonContent);

                sapi.Logger.Notification($"Loading weights from external configuration file: {jsonPath}");
                int processedBlocks = 0;
                int processedItems = 0;

                // Traitement des blocks
                if (configData.Blocks != null)
                {
                    foreach (var item in configData.Blocks)
                    {
                        string itemCode = item.Key;
                        sapi.Logger.Debug($"Processing block entry: {itemCode}");
                        
                        // Gestion des wildcards
                        if (itemCode.EndsWith("*"))
                        {
                            string prefix = itemCode.TrimEnd('*');
                            foreach (var block in sapi.World.Blocks)
                            {
                                if (block?.Code != null && block.Code.ToString().StartsWith(prefix))
                                {
                                    ProcessBlockWeight(block);
                                    processedBlocks++;
                                }
                            }
                        }
                        else
                        {
                            var block = sapi.World.GetBlock(new AssetLocation(itemCode));
                            if (block != null)
                            {
                                ProcessBlockWeight(block);
                                processedBlocks++;
                            }
                        }
                    }
                }

                // Traitement des items (si présent dans le JSON)
                if (configData.Items != null)
                {
                    foreach (var item in configData.Items)
                    {
                        string itemCode = item.Key;
                        sapi.Logger.Debug($"Processing item entry: {itemCode}");
                        
                        // Gestion des wildcards
                        if (itemCode.EndsWith("*"))
                        {
                            string prefix = itemCode.TrimEnd('*');
                            foreach (var gameItem in sapi.World.Items)
                            {
                                if (gameItem?.Code != null && gameItem.Code.ToString().StartsWith(prefix))
                                {
                                    ProcessItemWeight(gameItem);
                                    processedItems++;
                                }
                            }
                        }
                        else
                        {
                            var gameItem = sapi.World.GetItem(new AssetLocation(itemCode));
                            if (gameItem != null)
                            {
                                ProcessItemWeight(gameItem);
                                processedItems++;
                            }
                        }
                    }
                }

                sapi.Logger.Notification($"Finished loading weights. Processed {processedItems} items and {processedBlocks} blocks");
                sapi.Logger.Notification($"Final counts - Items: {Config.WEIGHTS_FOR_ITEMS.Count}, Blocks: {Config.WEIGHTS_FOR_BLOCKS.Count}");
                sapi.StoreModConfig(Config, this.Mod.Info.ModID + ".json");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"Error loading external weights configuration: {ex.Message}");
                sapi.Logger.Error($"Stack trace: {ex.StackTrace}");
            }
        }
        public void FillWeightDictionary()
        {
            int itemCount = 0;
            int blockCount = 0;

            // Parcourt tous les items et applique le filtre avec un motif
            foreach (var item in sapi.World.Items)
            {
                if (item?.Code == null) continue;

                // Traitement de chaque item avec ProcessItemWeight
                ProcessItemWeight(item);
                itemCount++;
            }

            sapi.Logger.Notification($"Processed {itemCount} items");

            // Parcourt tous les blocs et applique le filtre avec un motif
            foreach (var block in sapi.World.Blocks)
            {
                if (block?.Code == null) continue;
        
                // Traitement de chaque bloc avec ProcessBlockWeight
                ProcessBlockWeight(block);
                blockCount++;
            }
        
            sapi.Logger.Notification($"Processed {blockCount} blocks");
        
            // Bloc pour synchroniser les données réseau
            try
            {
                string tmpStr = JsonConvert.SerializeObject(itemIdToWeight, Formatting.Indented);
                bArrIITW = CompressStr(tmpStr);
        
                tmpStr = JsonConvert.SerializeObject(blockIdToWeight, Formatting.Indented);
                bArrBITW = CompressStr(tmpStr);
        
                tmpStr = JsonConvert.SerializeObject(itemBonusIdToWeight, Formatting.Indented);
                bArrIBITW = CompressStr(tmpStr);
        
                sapi.Logger.Notification($"Network sync data prepared: Items={itemIdToWeight.Count}, Blocks={blockIdToWeight.Count}, BonusItems={itemBonusIdToWeight.Count}");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"Error preparing network sync: {ex.Message}");
            }
        
            sapi.Logger.Notification("Finished filling weight dictionary");
        
            // Sauvegarde de la configuration dans le fichier JSON
            try
            {
                sapi.StoreModConfig(Config, this.Mod.Info.ModID + ".json");
                sapi.Logger.Notification("Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"Failed to save configuration: {ex.Message}");
            }
        }

        public void SendNewValues(IServerPlayer byPlayer)
        {
            sapi.Event.RegisterCallback((dt =>
            {
                if (byPlayer.ConnectionState == EnumClientState.Playing)
                {
                    serverChannel.SendPacket(new syncWeightPacket()
                    {
                        iITW = bArrIITW,
                        bITW = bArrBITW,
                        iBITW = bArrIBITW
                    }, byPlayer);
                }
            }), 20 * 1000);
        }

        public string CompressStr(string inStr)
        {
            byte[] compressedBytes;
            using (var uncompressedStream = new MemoryStream(Encoding.UTF8.GetBytes(inStr)))
            {
                using (var compressedStream = new MemoryStream())
                {
                    using (var compressorStream = new DeflateStream(compressedStream, CompressionLevel.Fastest, true))
                    {
                        uncompressedStream.CopyTo(compressorStream);
                    }
                    compressedBytes = compressedStream.ToArray();
                }
            }
            return Convert.ToBase64String(compressedBytes);
        }

        public static string Decompress(string compressedString)
        {
            byte[] decompressedBytes;
            var compressedStream = new MemoryStream(Convert.FromBase64String(compressedString));
            using (var decompressorStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                using (var decompressedStream = new MemoryStream())
                {
                    decompressorStream.CopyTo(decompressedStream);
                    decompressedBytes = decompressedStream.ToArray();
                }
            }
            return Encoding.UTF8.GetString(decompressedBytes);
        }

        public void OnServerExit()
        {
            // Cleanup if needed
        }

        public static void LoadClassBonusesMap()
        {
            foreach (string it in Config.CLASS_WEIGHT_BONUS.Split(';'))
            {
                if (it.Length != 0)
                {
                    string[] tmp = it.Split(':');
                    classBonuses.Add(tmp[0], float.Parse(tmp[1]));
                }
            }
        }

        public void LoadConfig()
        {
            try
            {
                sapi.Logger.Notification("Attempting to load configuration...");

                Config = sapi.LoadModConfig<Config>(this.Mod.Info.ModID + ".json");
                if (Config != null)
                {
                    sapi.Logger.Notification("Configuration loaded successfully.");
                    return;
                }

                sapi.Logger.Warning("Configuration file not found. Creating default configuration.");
            }
            catch (Exception ex)
            {
        sapi.Logger.Error($"Error loading configuration: {ex.Message}");
            }

            // Créer une configuration par défaut si le fichier n'existe pas
            Config = new Config();
            sapi.StoreModConfig(Config, this.Mod.Info.ModID + ".json");

            sapi.Logger.Notification("Default configuration created and saved.");
        }

        public override void Dispose()
        {
            if (harmonyInstance != null)
            {
                harmonyInstance.UnpatchAll(harmonyID);
            }

            Config = null;
            sapi = null;
            capi = null;
            mapLastCalculatedPlayerWeight = null;
            inventoryWasModified = null;
            classBonuses = null;
            itemIdToWeight = null;
            blockIdToWeight = null;
            itemBonusIdToWeight = null;
            bArrIITW = null;
            bArrBITW = null;
            bArrIBITW = null;
            harmonyInstance = null;
            serverChannel = null;
            clientChannel = null;
        }

        static readonly DateTime start = new DateTime(1970, 1, 1);
        public static long GetEpochSeconds()
        {
            return (long)((DateTime.UtcNow - start).TotalSeconds);
        }
    }
}