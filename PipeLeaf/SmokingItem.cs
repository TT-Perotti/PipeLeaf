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
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System.Numerics;
using Vintagestory.API.Server;

namespace PipeLeaf
{
    public class SmokingItem : Item
    {
        ILoadedSound cracklingSound;

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "smokingInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Attributes?.IsTrue("smokable") == true)
                    {
                        stacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "game:heldhelp-chargebow",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }

        protected static ItemSlot GetNextSmokable(EntityAgent byEntity)
        {
            ItemSlot slot = null;
            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                if (invslot.Itemstack != null && invslot.Itemstack.Collectible.Attributes?.IsTrue("smokable") == true)
                {
                    slot = invslot;
                    return false;
                }

                return true;
            });
            return slot;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            byEntity.World.Api.Logger.Debug("STARTING PIPE SMOKE");

            if (blockSel != null) return;
            if (byEntity.Controls.ShiftKey) return;

            ItemSlot smokableSlot = GetNextSmokable(byEntity);
            if (smokableSlot == null) return;

            if (byEntity.Swimming == true) return;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                {
                    byEntity.World.RegisterCallback(After1000ms, 1000);
                }
            }

            handling = EnumHandHandling.PreventDefault;
        }

        private void After1000ms(float delay)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            IClientPlayer plr = capi.World.Player;
            EntityPlayer plrentity = plr.Entity;
            capi.World.Api.Logger.Debug("starting sound");

            if (plrentity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                if (cracklingSound == null)
                    {
                        cracklingSound = capi.World.LoadSound(new SoundParams()
                        {
                            Location = new AssetLocation("sounds/effect/embers.ogg"),
                            ShouldLoop = true,
                            RelativePosition = true,
                            Position = new Vec3f(),
                            DisposeOnFinish = false,
                            Volume = 10.0f,
                            Range = 8
                        });
                        cracklingSound.Start();
                    }
            return;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.World.Api.Logger.Debug("StePPING PIPE SMOKE {0}", secondsUsed);
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return false;
            byEntity.World.Api.Logger.Debug("Player not null");


            ItemSlot smokableSlot = GetNextSmokable(byEntity);
            if (smokableSlot == null) return false;
            byEntity.World.Api.Logger.Debug("smokable items not null");

            if (blockSel != null) return false;

            // glowing animation and smoke animation?          

            byEntity.World.Api.Logger.Debug("Done with step");
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.World.Api.Logger.Debug("STOPPING PIPE SMOKE {0}", secondsUsed);

            cracklingSound?.Stop();
            cracklingSound?.Dispose();
            cracklingSound = null;

            ItemSlot smokableSlot = GetNextSmokable(byEntity);
            if (smokableSlot == null) return;

            if (secondsUsed > 4.5f)
            {
                byEntity.World.Api.Logger.Debug("YOU ARE SMOKING TOO HARD AND TAKIGN DAMANGE");
                OveruseDamage(byEntity, secondsUsed);
            }
            else
            {
                byEntity.World.Api.Logger.Debug("YOU TOOK A RESPONSIBLE DRAG");
                ResponsibleUseEffects(byEntity, secondsUsed);
                // apply player stat effects scaling with secondsUsed. Up to a max threshold.
            }

            //int n_used = 0;

            // scale n_used by secondsUsed

            {
                ItemStack stack = smokableSlot.TakeOut((int)secondsUsed);
                smokableSlot.MarkDirty();
            }

        }

        public static void ResponsibleUseEffects(EntityAgent effectedEntity, float secondsUsed)
        {
            effectedEntity.World.Api.Logger.Debug("Applying beneficial effects");
            IServerPlayer player = (
                effectedEntity.World.PlayerByUid((effectedEntity as EntityPlayer).PlayerUID)
                as IServerPlayer
            );
            // increate body temp

            var bh = effectedEntity.GetBehavior<EntityBehaviorBodyTemperature>();
            effectedEntity.World.Api.Logger.Debug("Current body temp {0}", bh.CurBodyTemperature);
            if (Math.Abs(bh.CurBodyTemperature) < 50)
                {
                player?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "Smoking has made you warm inside.",
                    EnumChatType.Notification
                );  
                bh.CurBodyTemperature += GameMath.Clamp(secondsUsed, 0, 5); 
                }

            // decrease hunger rate
            EntityStats stats = effectedEntity.Stats;
            
            stats.Set("hungerrate", "smokingmod", -1.0f, false);
            player?.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Your hunger rate has decreased slightly.",
                EnumChatType.Notification
            );
            effectedEntity.World.Api.Logger.Debug("Current hunger  rate {0}", stats.GetBlended("hungerrate"));

        }

        public static void OveruseDamage(EntityAgent smokingEntity, float secondsUsed)
        {
            smokingEntity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Poison
            }, Math.Abs(secondsUsed / 2));

        }
    }

}
