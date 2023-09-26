using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System.Numerics;
using Vintagestory.API.Server;
using System.Net;
using System.Runtime.CompilerServices;
using Vintagestory.ServerMods;
using Pipeleaf;

namespace PipeLeaf
{
    public class SmokableItem : Item
    {
        private Array effects;
        public override void OnLoaded(ICoreAPI api) 
        {
            base.OnLoaded(api);
            effects = Attributes?["smokableEffects"].AsArray();
        }

        public void Smoke(EntityAgent entity)
        {
            IServerPlayer player = (
                entity.World.PlayerByUid((entity as EntityPlayer).PlayerUID)
                as IServerPlayer
            );

            entity.World.Api.Logger.Debug($"Smoking {Code}");
            entity.World.Api.Logger.Debug($"Smoking Attributes: {Attributes}");
            entity.World.Api.Logger.Debug($"Smoking effects: {effects}");


            if (effects == null) { return; }
            player?.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                $"You feel the effects of smoking {Lang.Get(Code.ToString())}:",
                EnumChatType.Notification
            );
            foreach (JsonObject effect in effects)
            {
                string message = $"Your {effect["type"]} has been modified by {effect["amount"]}";

                switch (effect["type"].AsString())
                {
                    case "temporalstability":
                        message = "You feel focused and less inclined to insanity.";
                        break;
                    case "bodytemperature":
                        message = "You feel warm inside.";
                        break;
                    case "hungerrate":
                        message = "Your appetite is supressed.";
                        break;
                }

                player?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    message,
                    EnumChatType.Notification
                );

                TempEffect tempEffect = new();
                tempEffect.SetTempEntityStat(
                    (entity as EntityPlayer),
                    effect["type"].AsString(),
                    effect["amount"].AsFloat(),
                    effect["cooldown"].AsInt(),
                    "pipeleafmod",
                    effect["type"] + " pipeleafmod"
                    );
            }
        }
    }
}
