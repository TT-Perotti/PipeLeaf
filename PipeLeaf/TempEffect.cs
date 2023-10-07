using HarmonyLib;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Pipeleaf
{
    public class TempEffect
    {
        EntityPlayer effectedEntity;

        string effectType;
        
        float effectAmount;

        int effectCooldown;

        string effectCode;

        string effectId;

        public void SetTempEntityStat(
            EntityPlayer entity,
            string type,
            float amount,
            int cooldown,  // in seconds
            string code,
            string id
        )
        {
            effectedEntity = entity;
            effectType = type;
            effectAmount = amount;
            effectCooldown = cooldown;
            effectCode = code;
            effectId = id;

            effectedEntity.World.Api.Logger.Debug($"Setting temp stat: {effectType}, amount: {effectAmount}, cooldown: {effectCooldown}, code: {effectCode}");

            SetTempStat();

            if ( cooldown > 0 )
            {
                long effectIdCallback = effectedEntity.World.RegisterCallback(
                    ResetTempStat,
                    effectCooldown * 1000
                );
                effectedEntity.WatchedAttributes.SetLong(effectId, effectIdCallback);
            }
        }

        public void SetTempStat()
        {
            IServerPlayer player = (
                effectedEntity.World.PlayerByUid((effectedEntity as EntityPlayer).PlayerUID)
                as IServerPlayer
            );

            if (effectType == "bodytemperature")
            {
                EntityBehaviorBodyTemperature bh = effectedEntity.GetBehavior<EntityBehaviorBodyTemperature>();
                if ( bh.CurBodyTemperature <= 33 )
                {
                    bh.CurBodyTemperature += effectAmount;
                }
            }
            else if (effectType == "temporalstability")
            {
                double stability = effectedEntity.WatchedAttributes.GetDouble("temporalStability");
                if (stability < 1 - effectAmount)
                {
                    effectedEntity.WatchedAttributes.SetDouble("temporalStability", stability + effectAmount);
                }
                else
                {
                    effectedEntity.WatchedAttributes.SetDouble("temporalStability", 1);
                }
            }
            else
            {
                effectedEntity.Stats.Set(effectType, effectCode, effectAmount, false);

            }

            if (effectType == "maxhealthExtraPoints")
            {
                effectedEntity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();  
            }
        }

        public void ResetTempStat(float dt)
        {
            Reset();
        }

        public void Reset()
        {
            effectedEntity.Stats.Remove(effectType, effectCode);
            if (effectType == "maxhealthExtraPoints")
            {
                effectedEntity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
            }

            effectedEntity.WatchedAttributes.RemoveAttribute(effectId);
        }

        public void ResetAllTempStats(EntityPlayer entity, string effectCode)
        {
            foreach (var stats in entity.Stats)
            {
                entity.Stats.Remove(stats.Key, effectCode);
            }
            entity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }

        public void ResetAllAttrListeners(
            EntityPlayer entity,
            string callbackCode
        )
        {
            foreach (var watch in entity.WatchedAttributes.Keys)
            {
                if (watch.Contains(callbackCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
            }
        }

        public bool ResetAllListeners(EntityPlayer entity, string callbackCode)
        {
            bool effectReseted = false;
            foreach (var watch in entity.WatchedAttributes.Keys)
            {
                if (watch.Contains(callbackCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            effectReseted = true;
                            entity.World.UnregisterCallback(potionListenerId);
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
            }
            return effectReseted;
        }
    }
}