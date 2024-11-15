﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace WeightMod.src
{
    public class EntityBehaviorWeightable : EntityBehavior
    {
        bool shouldUpdate = true;
        bool changeMade = true;
        float accum = 0;
        float currentCalculatedWeight = 0;
        float lastCalculatedWeight = 0;
        ITreeAttribute weightTree;
        ITreeAttribute healthTree;
        float lastRatioHealth;
        float lastWeightBonusBags = 0;
        float currentWeightBonusBags = 0;
        float classBonus = 0;
        public bool shouldRecalc = false;
        Random random = new Random();
        public float lastWeightBonus
        {
            get { return weightTree.GetFloat("weightbonus"); }
            set
            {
                weightTree.SetFloat("weightbonus", value);
                entity.WatchedAttributes.MarkPathDirty("weightmod");
            }
        }
        public float lastPercentModifier
        {
            get { return weightTree.GetFloat("percentmodifier"); }
            set
            {
                weightTree.SetFloat("percentmodifier", value);
                entity.WatchedAttributes.MarkPathDirty("weightmod");
            }
        }
        public float maxWeight
        {
            get { return weightTree.GetFloat("maxweight"); }
            set
            {
                weightTree.SetFloat("maxweight", value);
                entity.WatchedAttributes.MarkPathDirty("weightmod");
            }
        }

        public float weight
        {
            get { return weightTree.GetFloat("currentweight"); }
            set { weightTree.SetFloat("currentweight", value); entity.WatchedAttributes.MarkPathDirty("weightmod"); }
        }
        public void PostInit()
        {
            weightTree = entity.WatchedAttributes.GetTreeAttribute("weightmod");
            healthTree = entity.WatchedAttributes.GetTreeAttribute("health");
            string playerClass = entity.WatchedAttributes.GetString("characterClass");
            if (playerClass == null || !WeightMod.Config.classBonuses.TryGetValue(playerClass, out classBonus))
            {
                classBonus = 0;
            }
            if (entity.World.Side == EnumAppSide.Server)
            {
                InventoryPlayerBackPacks playerBackpacks = (InventoryPlayerBackPacks)((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("backpack");

                InventoryBasePlayer playerHotbar = (InventoryBasePlayer)((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("hotbar");

                IInventory charakterInv = ((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("character");

                IInventory mouseInv = ((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("mouse");

                if (playerBackpacks == null || playerHotbar == null || charakterInv == null || mouseInv == null)
                {
                    WeightMod.sapi.Event.RegisterCallback((dt =>
                    {
                        playerBackpacks = (InventoryPlayerBackPacks)((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("backpack");

                        playerHotbar = (InventoryBasePlayer)((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("hotbar");

                        charakterInv = ((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("character");

                        mouseInv = ((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("mouse");

                        playerBackpacks.SlotModified += OnSlotModified;
                        playerHotbar.SlotModified += OnSlotModified;
                        charakterInv.SlotModified += OnSlotModified;
                        mouseInv.SlotModified += OnSlotModified;

                    }), 80 * 1000);
                }
                else
                {
                    playerBackpacks.SlotModified += OnSlotModified;
                    playerHotbar.SlotModified += OnSlotModified;
                    charakterInv.SlotModified += OnSlotModified;
                    mouseInv.SlotModified += OnSlotModified;
                }
            }

            if (weightTree == null)
            {
                entity.WatchedAttributes.SetAttribute("weightmod", weightTree = new TreeAttribute());

                weight = 0;// attributes["currentweight"].AsFloat(0);
                maxWeight = WeightMod.Config.MAX_PLAYER_WEIGHT;
                lastPercentModifier = 1;
                lastWeightBonusBags = 0;
                lastWeightBonus = 0;

                MarkDirty();
                return;
            }

            if (healthTree == null && entity.World.Side == EnumAppSide.Server)
            {
                WeightMod.sapi.Event.RegisterCallback((dt =>
                {
                    healthTree = entity.WatchedAttributes.GetTreeAttribute("health");
                    lastRatioHealth = healthTree.GetFloat("currenthealth") / healthTree.GetFloat("maxhealth");
                }), 80 * 1000);
                lastRatioHealth = 1;
            }
            else
            {
                lastRatioHealth = healthTree.GetFloat("currenthealth") / healthTree.GetFloat("maxhealth");
            }
            lastPercentModifier = weightTree.GetFloat("percentmodifier");
            weight = weightTree.GetFloat("currentweight");
            maxWeight = WeightMod.Config.MAX_PLAYER_WEIGHT;
            lastWeightBonusBags = 0;
            lastWeightBonus = weightTree.GetFloat("weightbonus");

            MarkDirty();
        }
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
        }
        public void MarkDirty()
        {
            entity.WatchedAttributes.MarkPathDirty("weightmod");
        }
        public EntityBehaviorWeightable(Entity entity) : base(entity)
        {

        }

        public bool isOverloaded()
        {
            var t = entity.Api;
            return this.weight > this.maxWeight;
        }
        public override string PropertyName()
        {
            return "affectedByItemsWeight";
        }
        public bool calculateWeightOfInventories()
        {
            //((this.entity as EntityPlayer).Player as IServerPlayer).SendMessage(0, "and again" + this.random.Next(), EnumChatType.Notification);
            if ((entity as EntityPlayer).Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                currentCalculatedWeight = 0;
                return false;
            }
            currentCalculatedWeight = 0;
            currentWeightBonusBags = 0;
            shouldUpdate = false;
            //Backpacks
            InventoryPlayerBackPacks playerBackpacks = (InventoryPlayerBackPacks)((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("backpack");
            //playerBackpacks.slot
            
            //playerBackpacks.Player
            if (playerBackpacks != null)
            {
                for (int i = 0; i < 4; ++i)
                {
                    if (playerBackpacks[i] != null)
                    {
                        ItemSlot itemSlot = playerBackpacks[i];
                        ItemStack itemStack = itemSlot.Itemstack;
                        if (itemStack != null)
                        {
                            if (itemStack.Collectible.Attributes != null && itemStack.Collectible.Attributes["weightbonusbags"].Exists)
                            {
                                currentWeightBonusBags += itemStack.Collectible.Attributes["weightbonusbags"].AsFloat();
                                continue;
                            }
                        }
                    }
                }

                {
                    for (int i = 4; i < playerBackpacks.Count; i++)
                    {
                        ItemSlot itemSlot = playerBackpacks[i];
                        if (itemSlot != null)
                        {
                            ItemStack itemStack = itemSlot.Itemstack;
                            if (itemStack != null)
                            {
                                if (itemStack.Collectible.Attributes != null && itemStack.Collectible.Attributes["weightmod"].Exists)
                                {
                                    currentCalculatedWeight += itemStack.Collectible.Attributes["weightmod"].AsFloat() * itemStack.StackSize;
                                    continue;
                                }
                            }
                        }
                    }
                }
            }
            

            //Hotbar
            InventoryBasePlayer playerHotbar = (InventoryBasePlayer)((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("hotbar");
            {
                if (playerHotbar != null)
                {
                    for (int i = 0; i < playerHotbar.Count; i++)
                    {
                        ItemSlot itemSlot = playerHotbar[i];
                        if (itemSlot != null)
                        {                        
                            ItemStack itemStack = itemSlot.Itemstack;
                            if (itemStack != null)
                            {
                                //petBackPackInventory
                                if(itemStack.Attributes.HasAttribute("petBackPackInventory"))
                                {
                                    var tree = itemStack.Attributes.GetTreeAttribute("petBackPackInventory");
                                    if(tree == null)
                                    {
                                        continue;
                                    }
                                    if(tree.HasAttribute("slots"))
                                    {
                                        if (tree.GetTreeAttribute("slots").Count > 0)
                                        {
                                            currentCalculatedWeight += 45000;
                                        }
                                    }
                                    continue;
                                }
                                if (itemStack.Collectible.Attributes != null && itemStack.Collectible.Attributes["weightmod"].Exists)
                                {
                                    currentCalculatedWeight += itemStack.Collectible.Attributes["weightmod"].AsFloat() * itemStack.StackSize;
                                    continue;
                                }
                            }
                        }
                    }
                }
            }
            //Character inventory
            IInventory charakterInv =  ((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("character");
            {
                if (charakterInv != null)
                {
                    for (int i = 0; i < charakterInv.Count; i++)
                    {
                        ItemSlot itemSlot = charakterInv[i];
                        if (itemSlot != null)
                        {
                            ItemStack itemStack = itemSlot.Itemstack;
                            if (itemStack != null)
                            {
                                if (itemStack.Item != null)
                                {
                                    if (itemStack.Collectible.Attributes != null && itemStack.Collectible.Attributes["weightmod"].Exists)
                                    {
                                        currentCalculatedWeight += itemStack.Collectible.Attributes["weightmod"].AsFloat() * itemStack.StackSize;
                                        continue;
                                    }
                                    if (itemStack.Collectible.Attributes != null && itemStack.Collectible.Attributes["weightbonusbags"].Exists)
                                    {
                                        currentWeightBonusBags += itemStack.Collectible.Attributes["weightbonusbags"].AsFloat();
                                        continue;
                                    }
                                }                               
                            }
                        }
                    }
                }
            }
           IInventory mouseInv = ((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("mouse");
            {
                if (mouseInv != null)
                {
                    for (int i = 0; i < mouseInv.Count; i++)
                    {
                        ItemSlot itemSlot = mouseInv[i];
                        if (itemSlot != null)
                        {
                            ItemStack itemStack = itemSlot.Itemstack;
                            if (itemStack != null)
                            {
                                if(itemStack.Attributes.HasAttribute("backpack"))
                                {
                                    var atrTmp = itemStack.Attributes.GetTreeAttribute("backpack");
                                    if(atrTmp.HasAttribute("slots"))
                                    {
                                        var slotsTmp = atrTmp.GetTreeAttribute("slots");

                                        foreach(ItemstackAttribute it in slotsTmp.Values)
                                        {
                                            if(it.value == null)
                                            {
                                                continue;
                                            }
                                            if(it.value.Class is EnumItemClass.Item)
                                            {
                                                if (WeightMod.itemIdToWeight.TryGetValue(it.value.Id, out float weight))
                                                {
                                                    currentCalculatedWeight += weight * it.value.StackSize;
                                                }
                                            }
                                            else if(it.value.Class is EnumItemClass.Block)
                                            {
                                                if (WeightMod.blockIdToWeight.TryGetValue(it.value.Id, out float weight))
                                                {
                                                    currentCalculatedWeight += weight * it.value.StackSize;
                                                }
                                            }

                                            
                                        }
                                    }

                                }

                            if (itemStack.Collectible.Attributes != null && itemStack.Collectible.Attributes["weightmod"].Exists)
                            {
                                currentCalculatedWeight += itemStack.Collectible.Attributes["weightmod"].AsFloat() * itemStack.StackSize;
                                continue;
                            }

                            }
                        }
                    }
                }
            }

            //craftinggrid
            IInventory craftingGridInv = ((this.entity as EntityPlayer).Player as IServerPlayer).InventoryManager.GetOwnInventory("craftinggrid");
            {
                if (craftingGridInv != null)
                {
                    for (int i = 0; i < craftingGridInv.Count; i++)
                    {
                        ItemSlot itemSlot = craftingGridInv[i];
                        if (itemSlot != null)
                        {
                            ItemStack itemStack = itemSlot.Itemstack;
                            if (itemStack != null)
                            {
                                if (itemStack.Attributes.HasAttribute("backpack"))
                                {
                                    var atrTmp = itemStack.Attributes.GetTreeAttribute("backpack");
                                    if (atrTmp.HasAttribute("slots"))
                                    {
                                        var slotsTmp = atrTmp.GetTreeAttribute("slots");

                                        foreach (ItemstackAttribute it in slotsTmp.Values)
                                        {
                                            if (it.value == null)
                                            {
                                                continue;
                                            }
                                            if (it.value.Class is EnumItemClass.Item)
                                            {
                                                if (WeightMod.itemIdToWeight.TryGetValue(it.value.Id, out float weight))
                                                {
                                                    currentCalculatedWeight += weight * it.value.StackSize;
                                                }
                                            }
                                            else if (it.value.Class is EnumItemClass.Block)
                                            {
                                                if (WeightMod.blockIdToWeight.TryGetValue(it.value.Id, out float weight))
                                                {
                                                    currentCalculatedWeight += weight * it.value.StackSize;
                                                }
                                            }


                                        }
                                    }

                                }

                                if (itemStack.Collectible.Attributes != null && itemStack.Collectible.Attributes["weightmod"].Exists)
                                {
                                    currentCalculatedWeight += itemStack.Collectible.Attributes["weightmod"].AsFloat() * itemStack.StackSize;
                                    continue;
                                }

                            }
                        }
                    }
                }
            }
            if (currentCalculatedWeight < 0)
            {
                currentCalculatedWeight = 0;
            }
            
            return shouldUpdate;
        }
        private void OnSlotModified(int i)
        {
            this.shouldRecalc = true;
        }
        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
             if (packetid == 6166)
            {
                ITreeAttribute treeAttribute = new TreeAttribute();
                SerializerUtil.FromBytes(data, (r) => treeAttribute.FromBytes(r));
                this.weight = treeAttribute.GetFloat("currentweight");
                this.maxWeight = treeAttribute.GetFloat("maxweight");
            }
        }
        public void updateWeight()
        {
            //currentCalculatedWeight updated
            calculateWeightOfInventories();

            ITreeAttribute treeAttribute = entity.WatchedAttributes.GetTreeAttribute("weightmod");
            
            if(treeAttribute == null)
            {
                return;
            }

            //if weight was changed
            if(!shouldUpdate && (currentCalculatedWeight - lastCalculatedWeight) > 20)
            {
                shouldUpdate = true;
            }
            // health ration changed
            if (!shouldUpdate && (lastPercentModifier - treeAttribute.GetFloat("percentmodifier")) > 0.09)
            {
                changeMade = true;
                lastPercentModifier = treeAttribute.GetFloat("percentmodifier");
            }
            if (!shouldUpdate && lastWeightBonus != entity.Stats.GetBlended("weightmodweightbonus"))
            {
                changeMade = true;
                lastWeightBonus = entity.Stats.GetBlended("weightmodweightbonus");
            }

            if (!shouldUpdate && lastWeightBonusBags != currentWeightBonusBags)
            {
                changeMade = true;
                lastWeightBonusBags = currentWeightBonusBags;
            }

            if (changeMade || shouldUpdate)
            {
                if (!WeightMod.Config.PERCENT_MODIFIER_USED_ON_RAW_WEIGHT)
                    maxWeight = (WeightMod.Config.MAX_PLAYER_WEIGHT * lastRatioHealth + lastWeightBonusBags + lastWeightBonus + classBonus) * lastPercentModifier;
                else
                    maxWeight = WeightMod.Config.MAX_PLAYER_WEIGHT * lastPercentModifier * lastRatioHealth + lastWeightBonusBags + lastWeightBonus + classBonus;
            }

            if (maxWeight < WeightMod.Config.MAX_PLAYER_WEIGHT * WeightMod.Config.RATIO_MIN_MAX_WEIGHT_PLAYER_HEALTH)
            {
                maxWeight = WeightMod.Config.MAX_PLAYER_WEIGHT * WeightMod.Config.RATIO_MIN_MAX_WEIGHT_PLAYER_HEALTH;
            }

            if (currentCalculatedWeight > maxWeight)
            {
                //Processed in harmPatch (Prefix_DoApplyOnGround/Prefix_DoApplyInLiquid) using isOverloaded
                entity.Stats.Set("walkspeed", "weightmod", (float)(-2), true);
                if ((entity as EntityAgent).MountedOn != null)
                {
                    (entity as EntityAgent).TryUnmount();
                }
            }
            //when player is not overburden yet but currentweight is across threshold value from config,
            //slower movespeed
            else if (currentCalculatedWeight > maxWeight * WeightMod.Config.WEIGH_PLAYER_THRESHOLD)
            {
                entity.Stats.Set("walkspeed", "weightmod", (float)(-0.2 * (currentCalculatedWeight / maxWeight)), true);
            }
            else
            {
                entity.Stats.Set("walkspeed", "weightmod", 0);
            }


            //if health ratio was changed or current and last weight is not the same = send packet and also update
            //treeAttribute with maxweight and currentweight
            if (changeMade || lastCalculatedWeight != currentCalculatedWeight)
            {
                treeAttribute.SetFloat("currentweight", currentCalculatedWeight);
                treeAttribute.SetFloat("maxweight", maxWeight);
                entity.WatchedAttributes.MarkPathDirty("weightmod");
                (entity.Api as ICoreServerAPI).Network.SendEntityPacket((entity as EntityPlayer).Player as IServerPlayer, entity.EntityId, 6166, SerializerUtil.ToBytes((w) => treeAttribute.ToBytes(w)));
            }

            //current now last
            lastCalculatedWeight = currentCalculatedWeight;
            changeMade = false;
        }
        public override void OnGameTick(float deltaTime)
        {
            if (entity.World.Api.Side == EnumAppSide.Server)
            {
                accum += deltaTime;
                if (accum >= WeightMod.Config.HOW_OFTEN_RECHECK && shouldRecalc)
                {
                    shouldRecalc = false;
                    accum = 0;                   
                    updateWeight();
                }
            }
        }

       /* public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (this.entity.World.Side == EnumAppSide.Server)
            {
                updateWeight();
                if (currentCalculatedWeight < 0)
                {
                    currentCalculatedWeight = 0;
                }
                if ((entity as EntityPlayer).Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    currentCalculatedWeight = 0;
                }
            }
        }*/
        public override void OnEntityRevive()
        {
            if (this.entity.World.Side == EnumAppSide.Server)
            {
                updateWeight();
                if (currentCalculatedWeight < 0)
                {
                    currentCalculatedWeight = 0;
                }
                if ((entity as EntityPlayer).Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    currentCalculatedWeight = 0;
                }
            }
        }
        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);
            if(healthTree == null)
            {
                return;
            }
            float tmpHealthRation = healthTree.GetFloat("currenthealth") / healthTree.GetFloat("maxhealth");
            if (lastRatioHealth != tmpHealthRation)
            {
                shouldRecalc = true;
                changeMade = true;
                lastRatioHealth = tmpHealthRation;
            }
        }
    }
}
