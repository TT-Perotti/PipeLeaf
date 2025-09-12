using ProtoBuf;
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

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("SmokingItem", typeof(SmokingItem));
            api.RegisterItemClass("SmokableItem", typeof(SmokableItem));

            api.RegisterItemClass("WearablePipe", typeof(WearablePipe));
            api.RegisterItemClass("ItemMatch", typeof(ItemMatch));
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
            api.World.Logger.StoryEvent("Smoke ascending...");

        }

        public void ResetSmokingEffectsOnDeath(IServerPlayer player, DamageSource damageSource)
        {
            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "You feel the effects of smoking dissipate.",
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

            capi.Input.RegisterHotKey("smokepipe", "Smoke Pipe", GlKeys.R, HotkeyType.CharacterControls);
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
                capi.World.Logger.Notification("PipeLeaf: inhale started");
            }
            return true;
        }

        private void OnClientTick(float dt)
        {
            var eplr = capi.World.Player?.Entity;
            if (eplr == null) return;

            var stack = GetEquippedPipeStack(eplr);
            if (stack?.Item is WearablePipe pipe)
            {
                // 🔥 Always update burn-down timer while pipe is equipped
                pipe.UpdateBurn(stack, capi.World);

                if (isInhaling)
                {
                    // Only spawn particles if the pipe is lit
                    if (pipe.IsLit(stack, capi.World))
                    {
                        pipe.ExtendBurn(stack, capi.World, 1/120);
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
                        if (fail == "pipenotlit")
                        {
                            capi.TriggerIngameError(this, "pipenotlit", Lang.Get("Pipe has gone out."));
                        }
                        else if (fail == "pipeempty")
                        {
                            capi.TriggerIngameError(this, "pipeempty", Lang.Get("The pipe is empty."));
                        }
                    }
                    else
                    {
                        clientNet.SendPacket(new SmokePipePacket());
                    }
                }
            }
        }

        private void OnSmokePipePacket(IServerPlayer fromPlayer, SmokePipePacket packet)
        {
            var slot = GetEquippedPipeSlot(fromPlayer);
            var stack = slot?.Itemstack;
            if (stack == null || !(stack.Item is WearablePipe pipe)) return;

            string fail;
            if (!pipe.TrySmoke(sapi.World, fromPlayer.Entity, stack, out fail))
            {
                switch (fail)
                {
                    case "pipeempty": fromPlayer.SendIngameError("pipeempty", "Your pipe is empty."); break;
                    case "notsmokable": fromPlayer.SendIngameError("notsmokable", "That’s not smokable."); break;
                    case "notenough": fromPlayer.SendIngameError("notenough", "Not enough loaded."); break;
                    default: fromPlayer.SendIngameError("smokefail", "Couldn’t smoke."); break;
                }
            }
        }

        private ItemSlot GetEquippedPipeSlot(IServerPlayer splayer)
        {
            var wearInv = splayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (wearInv == null) return null;

            foreach (var slot in wearInv)
            {
                if (!slot.Empty && slot.Itemstack?.Item is WearablePipe)
                    return slot;
            }
            return null;
        }
        private ItemStack GetEquippedPipeStack(EntityPlayer eplr)
        {
            var owningPlayer = capi.World.PlayerByUid(eplr.PlayerUID);
            if (owningPlayer == null) return null;

            var wearInv = owningPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (wearInv == null) return null;

            foreach (var slot in wearInv)
            {
                if (!slot.Empty && slot.Itemstack?.Item is WearablePipe)
                    return slot.Itemstack;
            }
            return null;
        }
    }

}
   
