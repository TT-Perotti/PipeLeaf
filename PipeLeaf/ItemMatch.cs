using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PipeLeaf
{
    public class ItemMatch : Item
    {
        ILoadedSound cracklingSound;
        private int lastParticleStep = -1;
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!(byEntity is EntityPlayer eplr)) return;

            var charInv = eplr.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            var faceSlot = charInv[(int)EnumCharacterDressType.Face];
            if (faceSlot.Empty || !(faceSlot.Itemstack?.Item is WearablePipe pipe)) return;
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                byEntity.World.RegisterCallback(PlayCrackleSound, 0);
            }
            handling = EnumHandHandling.PreventDefault; // we’re handling RMB
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!(byEntity is EntityPlayer eplr)) return false;

            int currentStep = (int)(secondsUsed / 0.5f);

            var charInv = eplr.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            var faceSlot = charInv[(int)EnumCharacterDressType.Face];
            if (faceSlot.Empty || !(faceSlot.Itemstack?.Item is WearablePipe pipe)) return false;

            var world = byEntity.World;

            // Debug: always try particles while holding
            if (world.Api.Side == EnumAppSide.Client && secondsUsed > 0.1f)
            {
                var capi = world.Api as ICoreClientAPI;
                bool firstPerson = false;

                // Base off the player's head/eye
                var pos = byEntity.SidedPos;

                // Forward vector pointing the same way as the player's gaze
                var fwd = new Vec3f(
                    (float)(-Math.Sin(pos.Yaw) * Math.Cos(pos.Pitch)),
                    (float)(Math.Sin(pos.Pitch)),
                    (float)(-Math.Cos(pos.Yaw) * Math.Cos(pos.Pitch))
                );

                var mouth = pos.XYZ
                    .AddCopy(byEntity.LocalEyePos)
                    .AddCopy(new Vec3d(0, -0.15, 0)) // slightly lower for mouth
                    .AddCopy(fwd * 0.30f); // push forward


                var ember = new SimpleParticleProperties(
                    1, 1,                                        // very few
                    ColorUtil.ToRgba(160, 255, 140, 50),          // small orange glow, semi-transparent
                    mouth, mouth,
                    new Vec3f(0, 0.01f, 0),                       // almost stationary, just a tiny lift
                    new Vec3f(0.05f, 0.02f, 0.05f),             // minimal spread
                    0.05f, 0.07f,                                   // very short life (quick fade)
                    0.02f, 0.05f,                                 // tiny spark size
                    EnumParticleModel.Quad
                );
                ember.SelfPropelled = false;
                ember.WindAffected = false;
                ember.AddPos.Set(0.01, 0.01, 0.01);
                if (currentStep != lastParticleStep)
                {
                    lastParticleStep = currentStep;
                    // Spawn your particle here
                    world.SpawnParticles(ember);
                }

            }

            return true;
        }
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!(byEntity is EntityPlayer eplr)) return;
            var world = byEntity.World;
            var capi = world.Api as ICoreClientAPI;
            var charInv = eplr.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            var faceSlot = charInv[(int)EnumCharacterDressType.Face];
            if (faceSlot.Empty || !(faceSlot.Itemstack?.Item is WearablePipe pipe)) return;
            ItemStack loaded = pipe.GetLoaded(faceSlot.Itemstack, api);
            if ( loaded == null ) {
                if (capi != null)
                {
                    capi.TriggerIngameError(this, "pipeempty", Lang.Get("Pipe is empty."));
                }
            }
            cracklingSound?.Stop();
            cracklingSound?.Dispose();
            cracklingSound = null;
            // Actual lighting logic
            if (secondsUsed >= 2f)
            {
                if (pipe.TryLight(faceSlot.Itemstack, world))
                {
                    if (!slot.Itemstack.Item.Code.Path.StartsWith("pipelighter"))
                    {
                        slot.TakeOut(1); // consume match
                        slot.MarkDirty();
                    }
                    if (world.Api.Side == EnumAppSide.Server)
                    {
                        (eplr.Player as IServerPlayer)?.SendMessage(
                            GlobalConstants.GeneralChatGroup,
                            Lang.Get("pipeleaf:pipe-lit"),
                            EnumChatType.Notification);
                    }
                }
            }
        }
        private void PlayCrackleSound(float delay)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            IClientPlayer plr = capi.World.Player;
            EntityPlayer plrentity = plr.Entity;

            if (plrentity.Controls.HandUse == EnumHandInteract.HeldItemInteract && capi.World.Side == EnumAppSide.Client)
                if (cracklingSound == null)
                {
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
                    cracklingSound.Start();
                }
            return;
        }


    }
}