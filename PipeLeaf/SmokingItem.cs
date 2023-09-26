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
    public class SmokingItem : Item
    {
        ILoadedSound cracklingSound;

        JsonObject smokableProps;

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
                    if (obj.GetType() == typeof(SmokableItem))
                    {
                        stacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "pipeleaf:heldhelp-smokingitem",
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
                if (invslot.Itemstack != null && invslot.Itemstack.Collectible.GetType() == typeof(SmokableItem))
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
            //byEntity.Api.Logger.Debug("Starting Smoking Item interaction");

            if (handling == EnumHandHandling.PreventDefault) return;

            if (byEntity.Controls.ShiftKey) return;
            if (byEntity.Swimming == true) return;

            ItemSlot smokableSlot = GetNextSmokable(byEntity);
            //byEntity.Api.Logger.Debug($"Smokable Slot found : {smokableSlot}");

            if (smokableSlot == null) return;


            if (byEntity.World.Side == EnumAppSide.Client)
            {
                {
                    byEntity.World.RegisterCallback(PlayCrackleSound, 1000);
                }
            }

            handling = EnumHandHandling.PreventDefault;
        }

        private void PlayCrackleSound(float delay)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            IClientPlayer plr = capi.World.Player;
            EntityPlayer plrentity = plr.Entity;

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

        private static SimpleParticleProperties InitializeSmokeEffect()
        {
            SimpleParticleProperties smokeHeld;
            smokeHeld = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(50, 220, 220, 220),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.05f, 0.1f, -0.05f),
                new Vec3f(0.05f, 0.15f, 0.05f),
                1.5f,
                0,
                0.25f,
                0.35f,
                EnumParticleModel.Quad
            );
            smokeHeld.SelfPropelled = true;
            smokeHeld.AddPos.Set(0.1, 0.1, 0.1);

            return smokeHeld;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return false;
            //byEntity.Api.Logger.Debug("Stepping Smoking Item interaction");

            if (byEntity.World.Side == EnumAppSide.Client && secondsUsed > 2)
            {
                // byEntity.Api.Logger.Debug("Seconds used greater than 2 and client side");

                float sideWays = 0.35f;
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                if (world.Player.Entity == byEntity && world.Player.CameraMode != EnumCameraMode.FirstPerson)
                {
                    sideWays = 0f;
                }

                Vec3d pos =
                    byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.5f, 0)
                    .Ahead(0.33f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                    .Ahead(sideWays, 0, byEntity.Pos.Yaw + GameMath.PIHALF)
                ;
                SimpleParticleProperties smokeHeld = InitializeSmokeEffect();
                smokeHeld.MinPos = pos.AddCopy(-0.05, 0.3, -0.05);
                byEntity.World.SpawnParticles(smokeHeld);
            }
            if (secondsUsed > 8)
            {
                //byEntity.Api.Logger.Debug("Seconds used greater than 7, stopping interaction");

                return false;
            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            cracklingSound?.Stop();
            cracklingSound?.Dispose();
            cracklingSound = null;

            ItemSlot smokableSlot = GetNextSmokable(byEntity);
            if (smokableSlot == null) return;

            int itemsConsumed = GameMath.Min(smokableSlot.Itemstack.StackSize, (int)secondsUsed);
            //byEntity.Api.Logger.Debug($"Items Consumed {itemsConsumed}");

            if (itemsConsumed > 6)
            {
                OveruseDamage(byEntity);
            }
            else if (itemsConsumed > 2)
            {
                SmokableItem smokableItem = (SmokableItem) smokableSlot.Itemstack.Collectible;
                smokableItem.Smoke(byEntity);

                var ltud = new LongTermUseDebuff();
                ltud.Apply(byEntity);
            }

            smokableSlot.TakeOut(itemsConsumed);
            smokableSlot.MarkDirty();
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

        }

        private static void OveruseDamage(EntityAgent byEntity)
            {
                byEntity.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Internal,
                    Type = EnumDamageType.Poison
                }, 1);
            }
    }
}
