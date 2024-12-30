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
            entity.World.Api.Logger.Debug($"Smoking {Code}");
            entity.World.Api.Logger.Debug($"Smoking effects: {effects.ToString()}");

            // why are effects coming in null?
            if (effects == null) { return; }

            entity.World.Api.Logger.Debug($"Effects not null");


            IServerPlayer player = (entity as EntityPlayer).Player as IServerPlayer;

            string langCode = Code.Domain + ":item-" + Code.Path;
            player?.SendMessage(
                GlobalConstants.GeneralChatGroup,
                $"You feel the effects of smoking {Lang.Get(langCode)}:",
                EnumChatType.Notification
            );
            foreach (JsonObject effect in effects)
            {
                string message = $"Your {effect["type"]} stat has been modified by {effect["amount"]}";

                switch (effect["type"].AsString())
                {
                    case "temporalstability":
                        if ( effect["amount"].AsFloat() < 0 )
                        {
                            message = "The darkness surrounds you.";
                        }
                        else
                        {
                            message = "You feel focused and less inclined to insanity.";

                        }
                        break;
                    case "bodytemperature":
                        message = "You feel warm inside.";
                        break;
                    case "hungerrate":
                        if (effect["amount"].AsFloat() < 0)
                        {
                            message = "Your appetite is supressed.";
                        }
                        else
                        {
                            message = "Smoking made you hungrier.";

                        }
                        break;
                    case "tiredness":
                        message = "You feel a little napish.";
                        break;
                    case "intoxication":
                        message = "You feel kind of woozy.";
                        break;
                }

                player?.SendMessage(
                    GlobalConstants.GeneralChatGroup,
                    message,
                    EnumChatType.Notification
                );

                TempEffect tempEffect = new();
                tempEffect.SetTempEntityStat(
                    (entity as EntityPlayer),
                    effect["type"].AsString(),
                    effect["amount"].AsFloat(),
                    effect["cooldown"].AsInt(),
                    "pipeleafmod",  // stat code
                    effect["type"] + " pipeleafmod" // listener/callback id
                );
                LongTermUseDebuff ltud = new();
                ltud.Apply(entity);
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            dsc.AppendLine("\n" + Lang.Get("pipeleaf:smokable-effects"));
            if (effects != null)
            {
                foreach (JsonObject effect in effects)
                {
                    dsc.AppendLine(effect["type"].AsString());
                }
            }
        }
    }
}
