using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PipeLeaf
{
    public class ItemLighter : Item
    {
        string igniteAnimation;
        ILoadedSound cracklingSound;
        private int lastParticleStep = -1;

        public override void OnLoaded(ICoreAPI api)
        {
            igniteAnimation = Attributes?["igniteAnimation"].AsString("ignitefirestarter");
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
            EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity == null || byEntity.World == null) return;

            // --- 1. Attempt to light fire ---
            if (blockSel != null)
            {
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                if (block is IIgnitable ign)
                {
                    IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
                    if (byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                    {
                        EnumIgniteState state = ign.OnTryIgniteBlock(byEntity, blockSel.Position, 0);
                        if (state == EnumIgniteState.Ignitable || state == EnumIgniteState.NotIgnitablePreventDefault)
                        {
                            handling = EnumHandHandling.PreventDefault;
                            byEntity.AnimManager?.StartAnimation(igniteAnimation);

                            if (byEntity.World.Side == EnumAppSide.Client)
                                RegisterFirestarterSound(byEntity, byPlayer);

                            return; // firestarter handled
                        }
                    }
                }
            }

            // --- 2. Attempt to light pipe if no ignitable block selected ---
            if (!(byEntity is EntityPlayer eplr)) return;

            var charInv = eplr.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            var faceSlot = charInv[(int)EnumCharacterDressType.Face];
            if (faceSlot.Empty || !(faceSlot.Itemstack?.Item is WearablePipe pipe)) return;

            handling = EnumHandHandling.PreventDefault;

            if (byEntity.World.Side == EnumAppSide.Client)
                byEntity.World.RegisterCallback(PlayCrackleSound, 0);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity == null || byEntity.World == null) return false;

            // --- Firestarter block step ---
            if (blockSel != null)
            {
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

                if (block is IIgnitable ign && byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                {
                    EnumIgniteState state = ign.OnTryIgniteBlock(byEntity, blockSel.Position, secondsUsed);
                    if (state == EnumIgniteState.Ignitable)
                    {
                        if (byEntity.World is IClientWorldAccessor)
                            SpawnFireParticles(blockSel, byEntity);
                        return true;
                    }
                    return false; // not ignitable
                }
            }

            // --- Pipe lighting step ---
            if (!(byEntity is EntityPlayer eplr)) return false;
            var charInv2 = eplr.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            var faceSlot2 = charInv2[(int)EnumCharacterDressType.Face];
            if (faceSlot2.Empty || !(faceSlot2.Itemstack?.Item is WearablePipe pipe2)) return false;

            int currentStep = (int)(secondsUsed / 0.5f);
            if (currentStep != lastParticleStep)
            {
                lastParticleStep = currentStep;
                if (byEntity.World != null)
                    SpawnPipeParticles(byEntity);
            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity == null || byEntity.World == null) return;
            byEntity.AnimManager?.StopAnimation(igniteAnimation);

            bool isLightingFire = false;

            // --- Firestarter block logic first ---
            if (blockSel != null)
            {
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                IIgnitable ign = block as IIgnitable;

                if (ign != null && ign.OnTryIgniteBlock(byEntity, blockSel.Position, secondsUsed) == EnumIgniteState.IgniteNow)
                {
                    isLightingFire = true;

                    if (api.World.Side != EnumAppSide.Client)
                    {
                        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
                        if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)) return;

                        EnumHandling handled = EnumHandling.PassThrough;
                        ign.OnTryIgniteBlockOver(byEntity, blockSel.Position, secondsUsed, ref handled);
                        if (!slot.Itemstack.Item.Code.Path.StartsWith("pipelighter"))
                        {
                            slot.TakeOut(1);
                            slot.MarkDirty();
                        }
                    }

                    SafeUnregisterFirestarterSound();
                }
            }

            // --- Pipe lighting only if NOT lighting a fire ---
            if (!isLightingFire && byEntity is EntityPlayer eplr)
            {
                var charInv = eplr.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                var faceSlot = charInv[(int)EnumCharacterDressType.Face];
                if (!faceSlot.Empty && faceSlot.Itemstack?.Item is WearablePipe pipe)
                {
                    try
                    {
                        cracklingSound?.Stop();
                        cracklingSound?.Dispose();
                        cracklingSound = null;
                    }
                    catch { } // ignore disposal errors

                    if (secondsUsed >= 2f && pipe.TryLight(faceSlot.Itemstack, byEntity.World))
                    {
                        if (!slot.Itemstack.Item.Code.Path.StartsWith("pipelighter"))
                        {
                            slot.TakeOut(1);
                            slot.MarkDirty();
                        }

                        if (byEntity.World.Side == EnumAppSide.Server)
                            (eplr.Player as IServerPlayer)?.SendMessage(
                                GlobalConstants.GeneralChatGroup,
                                Lang.Get("pipeleaf:pipe-lit"),
                                EnumChatType.Notification);
                    }
                }
            }
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity?.AnimManager?.StopAnimation(igniteAnimation);
            SafeUnregisterFirestarterSound();
            return true;
        }

        private void PlayCrackleSound(float delay)
        {
            if (!(api is ICoreClientAPI capi)) return;
            if (!(capi.World.Player?.Entity is EntityPlayer plrentity)) return;

            if (plrentity.Controls.HandUse != EnumHandInteract.HeldItemInteract || cracklingSound != null) return;

            cracklingSound = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/effect/embers"),
                ShouldLoop = true,
                RelativePosition = true,
                Position = new Vec3f(),
                DisposeOnFinish = false,
                Volume = 10.0f,
                Range = 8
            });

            cracklingSound?.Start();
        }

        private void SpawnFireParticles(BlockSelection blockSel, EntityAgent byEntity)
        {
            if (byEntity.World == null) return;

            Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
            Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1].Clone();
            props.basePos = pos;
            props.Quantity.avg = 0.3f;
            props.Size.avg = 0.03f;

            byEntity.World.SpawnParticles(props, byPlayer);
        }

        private void SpawnPipeParticles(EntityAgent byEntity)
        {
            var pos = byEntity.SidedPos;
            var fwd = new Vec3f(
                (float)(-Math.Sin(pos.Yaw) * Math.Cos(pos.Pitch)),
                (float)(Math.Sin(pos.Pitch)),
                (float)(-Math.Cos(pos.Yaw) * Math.Cos(pos.Pitch))
            );

            var mouth = pos.XYZ.AddCopy(byEntity.LocalEyePos).AddCopy(new Vec3d(0, -0.15, 0)).AddCopy(fwd * 0.3f);

            var ember = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(160, 255, 140, 50),
                mouth, mouth,
                new Vec3f(0, 0.01f, 0),
                new Vec3f(0.05f, 0.02f, 0.05f),
                0.05f, 0.07f,
                0.02f, 0.05f,
                EnumParticleModel.Quad
            );

            ember.SelfPropelled = false;
            ember.WindAffected = false;
            ember.AddPos.Set(0.01, 0.01, 0.01);

            byEntity.World?.SpawnParticles(ember);
        }

        private void RegisterFirestarterSound(EntityAgent byEntity, IPlayer byPlayer)
        {
            if (!(byEntity.World.Api is ICoreClientAPI capi)) return;

            SafeUnregisterFirestarterSound();

            capi.ObjectCache["firestartersound"] = capi.Event.RegisterCallback(
                dt => byEntity.World?.PlaySoundAt(new AssetLocation("sounds/walk/ice4"), byEntity, byPlayer, false, 16),
                500
            );
        }

        private void SafeUnregisterFirestarterSound()
        {
            if (!(api is ICoreClientAPI capi)) return;
            try
            {
                long cbid = ObjectCacheUtil.TryGet<long>(capi, "firestartersound");
                if (cbid != 0) capi.Event.UnregisterCallback(cbid);
            }
            catch { }
        }
    }
}
