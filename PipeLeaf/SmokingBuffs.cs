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
