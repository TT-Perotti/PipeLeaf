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
using System.Net;
using System.Runtime.CompilerServices;

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
                        ActionLangCode = "game:heldhelp-smoking",
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
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return false;


            ItemSlot smokableSlot = GetNextSmokable(byEntity);
            if (smokableSlot == null) return false;

            if (blockSel != null) return false;

            // glowing animation and smoke animation?          

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

            int itemsConsumed = GameMath.Min(smokableSlot.Itemstack.StackSize, (int)secondsUsed);

            if (itemsConsumed > 6)
            {
                OveruseDamage(byEntity, itemsConsumed);
            }
            else
            {
                ResponsibleUseEffects(byEntity, itemsConsumed);
                // apply player stat effects scaling with secondsUsed. Up to a max threshold.
            }

            if (byEntity.World.Side == EnumAppSide.Server)
            {
                var longTermUseDebuff = new LongTermUseDebuff();
                longTermUseDebuff.Apply(byEntity);
            }

            smokableSlot.TakeOut(itemsConsumed);
            smokableSlot.MarkDirty();


        }

        public static void ResponsibleUseEffects(EntityAgent byEntity, float secondsUsed)
        {
            IServerPlayer player = (
                byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID)
                as IServerPlayer
            );
            // increate body temp
            player?.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "You feel warmer inside.",
                EnumChatType.Notification
            );

            var bodyTempBuff = new BodyTempBuff();
            bodyTempBuff.Init(secondsUsed / 2);
            bodyTempBuff.Apply(byEntity);

            if (byEntity.Stats.GetBlended("hungerrate") > 0.05f && byEntity.World.Side == EnumAppSide.Server)
            {
                var hungerRateBuff = new HungerRateBuff();
                hungerRateBuff.Apply(byEntity);

                player?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    "You have less of an appetite.",
                    EnumChatType.Notification
                );
            }

        }

        //private void DecreaseHungerRate(EntityAgent byEntity)
        //{
        //    stats = byEntity.Stats;
        //    effectedEntity = byEntity;

        //    if (stats.GetBlended("hungerrate") > 0.05f && byEntity.World.Side == EnumAppSide.Server)
        //    {
        //        stats.Set("hungerrate", "smokingbuff", -0.05f, false);
        //        byEntity.WatchedAttributes.SetFloat("smokinghungerratebuff", -0.05f);
        //        long listenerId = byEntity.WatchedAttributes.GetLong("hungerratebuffdecaylistenerid", 0);
               
        //        //byEntity.World.Api.Logger.Debug($"hunger rate listener id is {listenerId}");
                
        //        if (listenerId == 0)
        //        {
        //            long effectIdGametick = byEntity.World.RegisterGameTickListener(decayHungerRateOneMinute, 60 * 1000);
        //            byEntity.WatchedAttributes.SetLong("hungerratebuffdecaylistenerid", effectIdGametick);
        //        }
        //    }
        //}        

        //private void decayHungerRateOneMinute(float dt)
        //{
        //    float curBuff = effectedEntity.WatchedAttributes.GetFloat("smokinghungerratebuff");
        //    float newBuff = curBuff + 0.01f;

        //    //effectedEntity.World.Api.Logger.Debug($"current buff is {curBuff}");
        //    //effectedEntity.World.Api.Logger.Debug($"updated buff is {newBuff}");


        //    if (newBuff >= 0)
        //    {
        //        stats.Remove("hungerrate", "smokingbuff");
        //        long listenerId = effectedEntity.WatchedAttributes.GetLong("hungerratebuffdecaylistenerid");
        //        effectedEntity.WatchedAttributes.SetLong("hungerratebuffdecaylistenerid", 0);
        //        effectedEntity.World.UnregisterGameTickListener(listenerId);
        //    }
        //    else
        //    {
        //        stats.Set("hungerrate", "smokingbuff", newBuff, false);
        //    }
        //    effectedEntity.WatchedAttributes.SetFloat("smokinghungerratebuff", newBuff);
        //}

        private static void OveruseDamage(EntityAgent smokingEntity, float secondsUsed)
            {
                smokingEntity.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Internal,
                    Type = EnumDamageType.Poison
                }, Math.Abs(secondsUsed / 6));
            }
    }
}
