using BuffStuff;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace PipeLeaf
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BodyTempBuff : Buff
    {
        public float increaseTempBy;
        public void Init(float increaseTempBy)
        { this.increaseTempBy = increaseTempBy; } 

        public override void OnStart()
        {
            EntityBehaviorBodyTemperature bh = Entity.GetBehavior<EntityBehaviorBodyTemperature>();
            if (Math.Abs(bh.CurBodyTemperature) < 34)
            {
                IServerPlayer player = (
                    Entity.World.PlayerByUid((Entity as EntityPlayer).PlayerUID)
                    as IServerPlayer
                );
                player?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "You feel warmer inside.",
                    EnumChatType.Notification
                );
                bh.CurBodyTemperature += GameMath.Min(increaseTempBy, 4);
            }
            SetExpiryImmediately();
        }

        public override void OnStack(Buff oldBuff)
        {
            EntityBehaviorBodyTemperature bh = Entity.GetBehavior<EntityBehaviorBodyTemperature>();
            if (Math.Abs(bh.CurBodyTemperature) < 34)
            {
                IServerPlayer player = (
                    Entity.World.PlayerByUid((Entity as EntityPlayer).PlayerUID)
                    as IServerPlayer
                );
                player?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "You feel warmer inside.",
                    EnumChatType.Notification
                );
                bh.CurBodyTemperature += GameMath.Min(increaseTempBy, 4);
                SetExpiryImmediately();
            }
        }

        public override void OnDeath()
        {
            SetExpiryImmediately();
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TemporalStabilityBuff : Buff
    {

        public Boolean buffApplied;

        public float stabilityBoost = 0.12f;
        public override void OnStart()
        {
            double stability = entity.WatchedAttributes.GetDouble("temporalStability");
            if (stability < 1 - stabilityBoost)
            {
                IServerPlayer player = (
                    Entity.World.PlayerByUid((Entity as EntityPlayer).PlayerUID)
                    as IServerPlayer
                );
                player?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    $"You feel focused and less inclined to insanity.",
                    EnumChatType.Notification
                );
                entity.WatchedAttributes.SetDouble("temporalStability", stability + stabilityBoost);
                buffApplied = true;
                SetExpiryInRealMinutes(15);
            }
            else
            {
                entity.WatchedAttributes.SetDouble("temporalStability", 1);
            }
        }

        public override void OnExpire()
        {
            if (buffApplied)
            {
                IServerPlayer player = (
                    Entity.World.PlayerByUid((Entity as EntityPlayer).PlayerUID)
                    as IServerPlayer
                );
                player?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "Your focus has begun to wane.",
                    EnumChatType.Notification
                );
            }
        }
    }

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class HungerRateBuff : Buff
    {
        float curBuff;
        public override void OnStart()
        {
            IServerPlayer player = (
                Entity.World.PlayerByUid((Entity as EntityPlayer).PlayerUID)
                as IServerPlayer
            );

            Entity.Stats.Set("hungerrate", "smokingbuff", -0.05f, false);
            curBuff = -0.05f;
            SetExpiryInRealMinutes(30);

            player?.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"Your appetite is supressed.",
                EnumChatType.Notification
            );
        }

        public override void OnStack(Buff oldBuff)
        {
            Entity.Stats.Set("hungerrate", "smokingbuff", -0.05f, false);
            curBuff = -0.05f;
            SetExpiryInRealMinutes(30);
        }

        public override void OnTick()
        {
            if (TickCounter % 24 == 0) // every six minutes reduce buff
            {
                float newBuff = curBuff + 0.01f;
                if (newBuff < 0)
                {
                    Entity.Stats.Set("hungerrate", "smokingbuff", newBuff, false);
                }
                else
                {
                    SetExpiryImmediately();
                }
            }
        }

        public override void OnExpire()
        {
            Entity.Stats.Remove("hungerrate", "smokingbuff");
            IServerPlayer player = (
                Entity.World.PlayerByUid((Entity as EntityPlayer).PlayerUID)
                as IServerPlayer
            );
            player?.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Your appetite is back to normal.",
                EnumChatType.Notification
            );
        }

        public override void OnDeath()
        {
            SetExpiryImmediately();
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class LongTermUseDebuff : Buff 
    {
        public List<Double> usages;
        public double LookbackWindowHours = 24 * 4;
        public Boolean debuffApplied;

        public override void OnStart()
        {
            usages = new List<Double>
            {
                Entity.World.Calendar.TotalHours
            };
            SetExpiryNever();
        }

        private float CalcUsageRate()
        { 
            return (float)usages.Count / (float)LookbackWindowHours;
        }

        public override void OnStack(Buff oldBuff)
        {
            var curHours = Entity.World.Calendar.TotalHours;
            //Entity.Api.Logger.Debug("Tracked smoking usage: {0}", usages);

            Boolean usagedCulled = CullUsagesOutsideLookbackWindow(curHours);
            if (curHours == usages?[-1]) { return; }
            LongTermUseDebuff myOldBuff = (LongTermUseDebuff)oldBuff;
            usages = myOldBuff.usages;
            usages.Add(Entity.World.Calendar.TotalHours);
            SetExpiryNever();


            if (usagedCulled)  // meaning weve hit a full lookback window
            {
                float usage_rate = CalcUsageRate();
                if (usage_rate > 0.20f) 
                {
                    if (!debuffApplied) 
                    {
                        IServerPlayer player = (
                            Entity.World.PlayerByUid((Entity as EntityPlayer).PlayerUID)
                            as IServerPlayer
                        );
                        player?.SendMessage(
                            GlobalConstants.InfoLogChatGroup,
                            "Smoking regularly leads to an early death! " +
                            "You experience a small penalty to your max health. " +
                            "Wait at least one game hour for the effects to dissipate.",
                            EnumChatType.Notification
                        );
                        Entity.Stats.Set("maxhealthExtraPoints", "smokingOveruse", -2, false);
                        debuffApplied = true;
                    }

                }
                else if (debuffApplied)
                {
                    Entity.Stats.Remove("maxhealthExtraPoints", "smokingOveruse");
                }
                EntityBehaviorHealth ebh = Entity.GetBehavior<EntityBehaviorHealth>();
                ebh.MarkDirty();
            }
        }

        private Boolean CullUsagesOutsideLookbackWindow(double curHours)
        {
            List<double> previousUsages = usages;
            double threshold = curHours - LookbackWindowHours;
            usages.RemoveAll(i => i < threshold );
            if (previousUsages.Count != usages.Count) { return true; }
            return false;
        }

        public override void OnDeath()
        {
            SetExpiryImmediately();
        }
    }
}
