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

namespace PipeLeaf
{
    public class SmokableItem : Item
    {
        public Array effects;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            effects = Attributes?["smokableEffects"].AsArray();
        }

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling)
        {
            if (!(byEntity is EntityPlayer eplr)) return;

            var api = byEntity.Api;

            if (!byEntity.Controls.ShiftKey) return;
            // Find equipped pipe
            var owningPlayer = api.World.PlayerByUid(eplr.PlayerUID);
            if (owningPlayer == null) return;

            handling = EnumHandHandling.PreventDefault;

            var wearInv = owningPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (wearInv == null) return;

            ItemSlot pipeSlot = null;
            foreach (var s in wearInv)
            {
                if (!s.Empty && s.Itemstack?.Item is WearablePipe)
                {
                    pipeSlot = s;
                    break;
                }
            }
            if (pipeSlot == null) return;

            var pipe = pipeSlot.Itemstack.Item as WearablePipe;
            string fail;
            if (pipe.TryLoadFrom(slot, pipeSlot, api, out fail))
            {
                handling = EnumHandHandling.PreventDefault; // suppress default use
                return;
            }

            // Client feedback
            var capi = api as ICoreClientAPI;
            if (capi != null)
            {
                switch (fail)
                {
                    case "notempty":
                        capi.TriggerIngameError(this, fail, "You must finish smoking before refilling.");
                        break;
                    case "notsmokable":
                        capi.TriggerIngameError(this, fail, "That can't be smoked.");
                        break;
                    case "emptysource":
                        capi.TriggerIngameError(this, fail, "You’re not holding anything to load.");
                        break;
                    case "notenough":
                        capi.TriggerIngameError(this, fail, "You must have at least 3 shag to load.");
                        break;
                    default:
                        capi.TriggerIngameError(this, fail, "Cannot load pipe.");
                        break;
                }
            }
        }

        public void Smoke(IWorldAccessor world, EntityAgent entity)
        {
            world.Api.Logger.Debug($"Smoking {Code}");
            world.Api.Logger.Debug($"Smoking effects: {effects.ToString()}");

            // why are effects coming in null?
            if (effects == null) { return; }

            world.Api.Logger.Debug($"Effects not null");


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
                    string type = effect["type"].AsString();
                    double amount = effect["amount"].AsDouble(0);

                    string sign = amount > 0 ? "+" : (amount < 0 ? "-" : "");

                    // If you just want the label with sign:
                    dsc.AppendLine($"{sign}{type}");

                    // Or, if you also want to show the numeric value:
                    // dsc.AppendLine($"{sign}{type} ({amount})");
                }
            }
        }
    }
}
