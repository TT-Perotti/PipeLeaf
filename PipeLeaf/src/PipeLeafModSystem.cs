using PipeLeaf.Items;
using ProtoBuf;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;



namespace PipeLeaf
{
    [ProtoContract]   // <-- required
    public class SmokePipePacket
    {
        [ProtoMember(1)]          // Field index must be unique and >0
        public double held { get; set; }
    }
    public class PipeLeafModSystem : ModSystem
    {
        // Called on server and client
        // Useful for registering block/entity classes on both sides

        IClientNetworkChannel clientNet;
        IServerNetworkChannel serverNet;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        KeyCombination lastSmokeCombo;
        double nextTooltipRefreshMs;
        int inhaleParticleTickCounter = 0;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("SmokableItem", typeof(SmokableItem));

            api.RegisterItemClass("WearablePipe", typeof(WearablePipe));
            api.RegisterItemClass("ItemLighter", typeof(ItemLighter));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            serverNet = sapi.Network
                .RegisterChannel("pipeleaf")
                .RegisterMessageType<SmokePipePacket>()
                .SetMessageHandler<SmokePipePacket>(OnSmokePipePacket);

            api.Event.PlayerDeath += ResetSmokingEffectsOnDeath;
            api.Event.RegisterGameTickListener(OnServerTick, 1000); // once per second

            api.World.Logger.StoryEvent(Lang.Get("pipeleaf:storyevent-smoke-ascending"));

        }

        public void ResetSmokingEffectsOnDeath(IServerPlayer player, DamageSource damageSource)
        {
            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                Lang.Get("pipeleaf:pipe-effect-dissipates"),
                EnumChatType.Notification
            );
            TempEffect tempEffect = new();
            tempEffect.ResetAllTempStats((player.Entity as EntityPlayer), "pipeleafmod");
            tempEffect.ResetAllListeners((player.Entity as EntityPlayer), "pipeleafmod");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            clientNet = capi.Network
                .RegisterChannel("pipeleaf")
                .RegisterMessageType<SmokePipePacket>();

            capi.Input.RegisterHotKey("smokepipe", Lang.Get("pipeleaf:hotkey-smokepipe"), GlKeys.R, HotkeyType.CharacterControls);
            capi.Input.SetHotKeyHandler("smokepipe", OnSmokePipePressed);

            // Poll for release + show inhale particles
            capi.Event.RegisterGameTickListener(OnClientTick, 50);   // ~20x/sec
        }

        double inhaleStartTime = -1;
        bool isInhaling = false;
        private bool OnSmokePipePressed(KeyCombination comb)
        {
            // Fires continuously while held — so only act on first tick
            if (!isInhaling)
            {
                inhaleStartTime = capi.World.ElapsedMilliseconds;
                isInhaling = true;
                // capi.World.Logger.Notification("PipeLeaf: inhale started");
            }
            return true;
        }
        private void OnServerTick(float dt)
        {
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                var splayer = player as IServerPlayer;
                var slot = GetEquippedPipeSlot(splayer);
                var stack = slot?.Itemstack;
                if (stack?.Item is WearablePipe pipe)
                {
                    pipe.UpdateBurn(stack, sapi.World, splayer.Entity);
                }
            }
        }

        private void OnClientTick(float dt)
        {
            var eplr = capi.World.Player?.Entity;
            if (eplr == null) return;

            inhaleParticleTickCounter++;
            var stack = GetEquippedPipeStack(eplr);
            if (stack?.Item is WearablePipe pipe && isInhaling)
            {
                if (pipe.IsLit(stack, capi.World))
                {
                    pipe.ExtendBurn(stack, capi.World, 2.0 / 60.0);
                    if (inhaleParticleTickCounter >= 5)
                    {
                        inhaleParticleTickCounter = 0;
                        pipe.SpawnInhaleParticles(capi.World, eplr);
                    }
                }
            }

            // Detect release manually
            var hotkey = capi.Input.HotKeys["smokepipe"];
            if (hotkey == null) return;

            if (isInhaling && !capi.Input.KeyboardKeyStateRaw[(int)hotkey.CurrentMapping.KeyCode])
            {
                double held = capi.World.ElapsedMilliseconds - inhaleStartTime;
                isInhaling = false;
                inhaleStartTime = -1;

                if (held >= 2000 && stack?.Item is WearablePipe litPipe)
                {
                    string fail;
                    if (!litPipe.TrySmoke(capi.World, eplr, stack, out fail))
                    {
                        if (capi.World.Side == EnumAppSide.Client)
                        {
                            if (fail == "pipenotlit")
                            {
                                capi.TriggerIngameError(this, "pipenotlit", Lang.Get("pipeleaf:ingameerror-pipenotlit"));
                            }
                            else if (fail == "pipeempty")
                            {
                                capi.TriggerIngameError(this, "pipeempty", Lang.Get("pipeleaf:ingameerror-pipeempty"));
                            }
                        }

                    }
                    else
                    {
                        clientNet.SendPacket(new SmokePipePacket { held = held });
                    }
                }
            }
        }
        private void OveruseDamage(IServerPlayer player)
        {

            player?.SendMessage(
                GlobalConstants.GeneralChatGroup,
                Lang.Get("pipeleaf:overuse-warning"),
                EnumChatType.Notification
                );

            player.Entity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Poison
            }, Math.Abs(1));
        }

        private void OnSmokePipePacket(IServerPlayer fromPlayer, SmokePipePacket packet)
        {
            var slot = GetEquippedPipeSlot(fromPlayer);
            var stack = slot?.Itemstack;
            if (stack == null || !(stack.Item is WearablePipe pipe)) return;

            string fail;
            if (!pipe.TrySmoke(sapi.World, fromPlayer.Entity, stack, out fail))
            {
                // Server-side: send chat message / notification
                string msg;
                switch (fail)
                {
                    case "pipeempty": msg = "Your pipe is empty."; break;
                    case "pipenotlit": msg = "Your pipe has gone out."; break;
                    default: msg = "Couldn’t smoke."; break;
                }

                fromPlayer.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
                return;
            }

            // Overuse damage if held too long
            if (packet.held >= 7000)
            {
                OveruseDamage(fromPlayer);
            }
            slot.MarkDirty();

        }


        private ItemSlot GetEquippedPipeSlot(IServerPlayer splayer)
        {
            var wearInv = splayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (wearInv == null) return null;

            // Face slot is always at index of EnumCharacterDressType.Face
            var faceSlot = wearInv[(int)EnumCharacterDressType.Face];
            return (!faceSlot.Empty && faceSlot.Itemstack?.Item is WearablePipe) ? faceSlot : null;
        }

        private ItemStack GetEquippedPipeStack(EntityPlayer eplr)
        {
            var owningPlayer = capi.World.PlayerByUid(eplr.PlayerUID);
            if (owningPlayer == null) return null;

            var wearInv = owningPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (wearInv == null) return null;

            var faceSlot = wearInv[(int)EnumCharacterDressType.Face];
            return (!faceSlot.Empty && faceSlot.Itemstack?.Item is WearablePipe) ? faceSlot.Itemstack : null;
        }
    }

}

