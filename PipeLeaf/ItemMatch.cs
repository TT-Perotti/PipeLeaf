using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PipeLeaf
{
    public class ItemMatch : Item
    {
        ILoadedSound cracklingSound;
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

            var charInv = eplr.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            var faceSlot = charInv[(int)EnumCharacterDressType.Face];
            if (faceSlot.Empty || !(faceSlot.Itemstack?.Item is WearablePipe pipe)) return false;

            var world = byEntity.World;

            // Debug: always try particles while holding
            if (world.Api.Side == EnumAppSide.Client && secondsUsed > 0.1f)
            {
                var capi = world.Api as ICoreClientAPI;
                bool firstPerson = false;
                if (capi != null)
                {
                    firstPerson = (capi.World.Player.CameraMode == EnumCameraMode.FirstPerson);
                }

                // Base off the player's head/eye
                Vec3d pos = byEntity.SidedPos.XYZ.AddCopy(byEntity.LocalEyePos);

                // Adjust differently depending on camera mode
                if (firstPerson)
                {
                    // Bring it close to camera and slightly down (like holding to mouth)
                    pos.Add(-0.04, -0.10, 0.06);
                }
                else
                {
                    // Third person: move it forward from the face slot area
                    Vec3f fwd = new Vec3f(
                        (float)-Math.Sin(byEntity.SidedPos.Yaw),
                        0,
                        (float)Math.Cos(byEntity.SidedPos.Yaw)
                    );
                    fwd.Normalize();

                    pos.Add(fwd.X * 0.1f, -0.15, fwd.Z * 0.1f);
                }

                var ember = new SimpleParticleProperties(
                    1, 2,                                        // very few
                    ColorUtil.ToRgba(160, 255, 140, 50),          // small orange glow, semi-transparent
                    pos, pos,
                    new Vec3f(0, 0.01f, 0),                       // almost stationary, just a tiny lift
                    new Vec3f(0.005f, 0.02f, 0.005f),             // minimal spread
                    0.1f, 0.2f,                                   // very short life (quick fade)
                    0.02f, 0.05f,                                 // tiny spark size
                    EnumParticleModel.Quad
                );
                ember.SelfPropelled = false;
                ember.WindAffected = false;
                ember.AddPos.Set(0.01, 0.01, 0.01);

                world.SpawnParticles(ember);

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