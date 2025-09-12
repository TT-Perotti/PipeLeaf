using System;
using System.Collections;
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
    public class LongTermUseDebuff 
    {
        EntityAgent entity;
        public int LookbackWindowHours = 24;
        public float UsageRateAtDebuff = 0.20f;
        public Boolean debuffApplied= false;

        public void Apply(EntityAgent Entity)
        {
            entity = Entity;
            List<int> usages = GetUsages();

            int curHours = (int)Entity.World.Calendar.TotalHours;
            Entity.Api.Logger.Debug(string.Join(", ", usages));

            if (curHours == usages.LastOrDefault(0)) { return; }
            Boolean usagesCulled = CullUsagesOutsideLookbackWindow(curHours, usages);
            usages.Add(curHours);
            Entity.Api.Logger.Debug(string.Join(", ", usages));


            if (usagesCulled)  // meaning weve hit a full lookback window
            {
                float usageRate = CalcUsageRate(usages);
                Entity.Api.Logger.Debug($"Usages were culled, calculating usage rate: {usageRate}");

                if (usageRate > UsageRateAtDebuff)
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
                    TempEffect tempEffect = new();
                    tempEffect.SetTempEntityStat(
                        (entity as EntityPlayer),
                        "maxhealthExtraPoints",
                        -4,
                        60 * 60,
                        "pipeleafmod",
                        "maxhealthExtraPoints" + "pipeleafmod"
                    );
                }
            }
            StoreUsages(usages);
        }

        private List<int> GetUsages()
        {
            var storedUsages = entity.WatchedAttributes.GetStringArray("smokingModUsages");
            //Entity.Api.Logger.Debug(string.Join(", ", storedUsages));

            if (storedUsages == null)
            {
                List<int> storedUsagesAsInt = new();
                return storedUsagesAsInt;
            }
            else 
            {
                List<string> storedUsageAsStrings = new List<string>(entity.WatchedAttributes.GetStringArray("smokingModUsages"));
                List<int> storedUsagesAsInt = storedUsageAsStrings.ConvertAll(int.Parse);
                return storedUsagesAsInt;
            }
        }

        private void StoreUsages(List<int> usages)
        {
            entity.WatchedAttributes.SetStringArray("smokingModUsages", usages.ConvertAll(x => x.ToString()).ToArray());
        }

        private float CalcUsageRate(List<int> usages)
        { 
            return usages.Count / LookbackWindowHours;
        }

        private Boolean CullUsagesOutsideLookbackWindow(int curHours, List<int> usages)
        {
            entity.Api.Logger.Debug("Culling usages");

            int previousUsagesCount = usages.Count;
            int thresholdHour = curHours - LookbackWindowHours;
            usages.RemoveAll(i => i <= thresholdHour );
            entity.Api.Logger.Debug($"Previous {previousUsagesCount}, cuurent {usages.Count}");
            return previousUsagesCount > usages.Count;
        }
    }
}
