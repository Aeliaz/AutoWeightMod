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
using ItemCategorizer = ItemCategorizer.src.Config

namespace WeightMod.src
{
    public class WeightMod : ModSystem
    {
    public static ICoreServerAPI sapi;
    public static ICoreClientAPI capi;
    private static Dictionary<string, float> mapLastCalculatedPlayerWeight = new Dictionary<string, float>();
    private static Dictionary<string, bool> inventoryWasModified = new Dictionary<string, bool>();
    private static Dictionary<string, float> classBonuses = new Dictionary<string, float>();
    public static Dictionary<int, float> itemIdToWeight = new Dictionary<int, float>();
    public static Dictionary<int, float> blockIdToWeight = new Dictionary<int, float>();
    public static Dictionary<int, float> itemBonusIdToWeight = new Dictionary<int, float>();
    public static string bArrIITW;
    public static string bArrBITW;
    public static string bArrIBITW;
    public static Harmony harmonyInstance;
    internal static IServerNetworkChannel serverChannel;
    internal static IClientNetworkChannel clientChannel;
    public const string harmonyID = "WeightMod.Patches";
    static WeightMod instance;
    public static Config Config { get; private set; } = null!;

        public void OnPlayerNowPlaying(IServerPlayer byPlayer)
        {
            var ep = new EntityBehaviorWeightable(byPlayer.Entity);
            ep.PostInit();
            byPlayer.Entity.AddBehavior(ep);
        }

        public void OnPlayerNowPlayingClient(IClientPlayer byPlayer)
        {
            // capi.World.Player
            // var ep = new EntityBehaviorWeightable(byPlayer.Entity);
            // ep.PostInit();
            // byPlayer.Entity.AddBehavior(ep);
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
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityInAir).GetMethod("Applicable"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_ApplicableInAir")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityInLiquid).GetMethod("Applicable"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_ApplicableInLiquid")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityOnGround).GetMethod("Applicable"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_ApplicableOnGround")));
        }

        public static WeightMod GetInstance()
        {
            return instance;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            itemIdToWeight = new Dictionary<int, float>();
            blockIdToWeight = new Dictionary<int, float>();
            itemBonusIdToWeight = new Dictionary<int, float>();

            capi = api;
            base.StartClientSide(api);

            LoadConfig();

            api.Gui.RegisterDialog(new HudWeightPlayer(api));
            harmonyInstance = new Harmony(harmonyID);
            harmonyInstance.Patch(typeof(Vintagestory.API.Common.CollectibleObject).GetMethod("GetHeldItemInfo"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Postfix_GetHeldItemInfo")));
            clientChannel = api.Network.RegisterChannel("weightmod");
            clientChannel.RegisterMessageType(typeof(syncWeightPacket));
            clientChannel.SetMessageHandler<syncWeightPacket>((packet) =>
            {
                Dictionary<int, float> tmpDict = JsonConvert.DeserializeObject<Dictionary<int, float>>(Decompress(packet.iITW));

                foreach (var item in capi.World.Items)
                {
                    if (tmpDict.TryGetValue(item.Id, out float val))
                    {
                        item.Attributes.Token["weightmod"] = val;
                    }
                }
                tmpDict = JsonConvert.DeserializeObject<Dictionary<int, float>>(Decompress(packet.bITW));
                foreach (var item in capi.World.Blocks)
                {
                    if (tmpDict.TryGetValue(item.Id, out float val))
                    {
                        if (item.Attributes != null)
                        {
                            item.Attributes.Token["weightmod"] = val;
                        }
                    }
                }
                tmpDict = JsonConvert.DeserializeObject<Dictionary<int, float>>(Decompress(packet.iBITW));
                foreach (var item in capi.World.Items)
                {
                    if (tmpDict.TryGetValue(item.Id, out float val))
                    {
                        if (item.Attributes != null)
                        {
                            item.Attributes.Token["weightbonusbags"] = val;
                        }
                    }
                }
            });
            capi.Event.PlayerJoin += OnPlayerNowPlayingClient;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            mapLastCalculatedPlayerWeight = new Dictionary<string, float>();
            inventoryWasModified = new Dictionary<string, bool>();
            itemIdToWeight = new Dictionary<int, float>();
            blockIdToWeight = new Dictionary<int, float>();
            itemBonusIdToWeight = new Dictionary<int, float>();
            sapi = api;
            base.StartServerSide(api);

            LoadConfig();

            LoadClassBonusesMap();
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnServerExit);
            api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, FillWeightDictionary);
            serverChannel = sapi.Network.RegisterChannel("weightmod");
            serverChannel.RegisterMessageType(typeof(syncWeightPacket));
            api.Event.PlayerNowPlaying += SendNewValues;
            harmonyInstance = new Harmony(harmonyID);
            harmonyInstance.Patch(typeof(Vintagestory.API.Common.InventoryBase).GetMethod("DidModifyItemSlot"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_OnItemSlotModified")));
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
                    },
                    byPlayer);
                }
            }), 20 * 1000);
        }

        public void FillWeightDictionary()
        {
            bool configModified = false;

            // Process all items
            foreach (var item in sapi.World.Items)
            {
                if (item?.Code == null) continue;

                string itemCode = $"{item.Code.Domain}:{item.Code.Path}";

                // Skip if already in config
                if (!Config.WEIGHTS_FOR_ITEMS.ContainsKey(itemCode))
                {
                    string category = ItemCategorizer.DetermineCategory(item);
                    float baseWeight = ItemCategorizer.CalculateBaseWeight(item, category, Config);

                    Config.WEIGHTS_FOR_ITEMS[itemCode] = new ItemWeightInfo
                    {
                        Weight = baseWeight,
                        Category = category
                    };
                    configModified = true;
                }

                // Apply weight to item if it has one
                var weightInfo = Config.WEIGHTS_FOR_ITEMS[itemCode];
                if (weightInfo.Weight.HasValue && item.Attributes != null)
                {
                    item.Attributes.Token["weightmod"] = weightInfo.Weight.Value;
                    item.Attributes = new JsonObject(item.Attributes.Token);
                    itemIdToWeight[item.Id] = weightInfo.Weight.Value;
                }
            }

            // Process all blocks
            foreach (var block in sapi.World.Blocks)
            {
                if (block?.Code == null) continue;

                string blockCode = $"{block.Code.Domain}:{block.Code.Path}";

                if (!Config.WEIGHTS_FOR_BLOCKS.ContainsKey(blockCode))
                {
                    string category = ItemCategorizer.DetermineCategory(block);
                    float baseWeight = ItemCategorizer.CalculateBaseWeight(block, category, Config);

                    Config.WEIGHTS_FOR_BLOCKS[blockCode] = new ItemWeightInfo
                    {
                        Weight = baseWeight,
                        Category = category
                    };
                    configModified = true;
                }

                var weightInfo = Config.WEIGHTS_FOR_BLOCKS[blockCode];
                if (weightInfo.Weight.HasValue && block.Attributes != null)
                {
                    block.Attributes.Token["weightmod"] = weightInfo.Weight.Value;
                    block.Attributes = new JsonObject(block.Attributes.Token);
                    blockIdToWeight[block.Id] = weightInfo.Weight.Value;
                }
            }

            // Save config if modified
            if (configModified)
            {
                sapi.StoreModConfig(Config, this.Mod.Info.ModID + ".json");
            }

            // Prepare network sync data
            string tmpStr = JsonConvert.SerializeObject(itemIdToWeight, Formatting.Indented);
            bArrIITW = CompressStr(tmpStr);
            tmpStr = JsonConvert.SerializeObject(blockIdToWeight, Formatting.Indented);
            bArrBITW = CompressStr(tmpStr);
            tmpStr = JsonConvert.SerializeObject(itemBonusIdToWeight, Formatting.Indented);
            bArrIBITW = CompressStr(tmpStr);
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
            // classBonuses.Clear();
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
                Config = sapi.LoadModConfig<Config>(this.Mod.Info.ModID + ".json");
                if (Config != null)
                {
                    return;
                }
            }
            catch
            {
                Config = new Config();
                sapi.StoreModConfig(Config, this.Mod.Info.ModID + ".json");
                return;
            }

            Config = new Config();
            sapi.StoreModConfig(Config, this.Mod.Info.ModID + ".json");
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

