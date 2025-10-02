using System;
using System.Numerics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace PipeLeaf.Items
{
    public class WearablePipe : ItemWearable
    {
        const string AttrPipeContents = "pipeContents";
        const string AttrLoad = "stack";

        const string AttrLitUntil = "pipeLitUntil";
        const string AttrLastBurn = "lastBurnCheck";
        const string AttrTotalLit = "pipeTotalLit";
        const string AttrNextEffectReady = "pipeNextEffectReady";
        const double BurnIncrementHours = 1;
        const double MaxTotalBurnHours = 5;
        const double effectCooldown = 1;
        private string lastDebugState = null;

        public bool IsLit(ItemStack stack, IWorldAccessor world)
        {
            // Only update burn-down on server
            if (world.Side == EnumAppSide.Server)
            {
                UpdateBurnLazy(stack, world);
            }

            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);
            if (double.IsNaN(litUntil) || litUntil < 0) litUntil = 0;
            double now = world.Calendar.TotalHours;

            return litUntil > now;
        }


        private void DebugChatState(ItemStack stack, ICoreAPI api, string context)
        {
            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);
            double totalLit = stack.Attributes.GetDouble(AttrTotalLit, 0);
            double nextEffect = stack.Attributes.GetDouble(AttrNextEffectReady, -1);
            double now = api.World.Calendar.TotalHours;

            string msg = $"[PipeDebug:{context}] \n" +
                         $"LitUntil={litUntil},\n Remaining={litUntil-now}, \n" +
                         $"TotalLit={totalLit}, \nNextEffectIn={nextEffect - now}s, \nNow={now}";

            // Only send to chat if state changed
            if (lastDebugState != context)
            {
                lastDebugState = context;

                if (api.Side == EnumAppSide.Client && api is ICoreClientAPI capi)
                {
                    capi.ShowChatMessage(msg);   // ✅ one-time chat update
                }
                else if (api is ICoreServerAPI sapi)
                {
                    sapi.Logger.Notification(msg);  // server log
                }
            }

            // Always write to console logs for deep debugging
            api.World.Logger.Notification(msg);
        }

        public bool TryLight(ItemStack stack, IWorldAccessor world, out string fail)
        {
            fail = null;

            ItemStack load = GetLoaded(stack, world.Api);
            if (load == null || load.StackSize <= 0)
            {
                fail = "pipe-empty";
                return false;
            }

            double totalLit = stack.Attributes.GetDouble(AttrTotalLit, 0);
            if (totalLit >= MaxTotalBurnHours)
            {
                fail = "pipe-maxburn";
                return false;
            }

            double now = world.Calendar.TotalHours;
            stack.Attributes.SetDouble(AttrLitUntil, now + BurnIncrementHours);
            stack.Attributes.SetDouble(AttrLastBurn, now);

            if (!stack.Attributes.HasAttribute(AttrNextEffectReady))
                stack.Attributes.SetDouble(AttrNextEffectReady, now);

            return true;
        }

        public void ExtendBurn(ItemStack stack, IWorldAccessor world, double add_time)
        {
            double now = world.Calendar.TotalHours;
            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);
            if (double.IsNaN(litUntil) || litUntil < 0) litUntil = 0;

            if (litUntil <= 0) return; // already out

            double newUntil = litUntil + add_time;
            // cap the possible extension to 2 minutes from now
            if (litUntil + add_time - now > 2) { newUntil = now + 2; }
            ;
            stack.Attributes.SetDouble(AttrLitUntil, newUntil);
        }

        public (double totalLit, double remainingHours, bool lit) GetBurnState(ItemStack stack, IWorldAccessor world)
        {
            double now = world.Calendar.TotalHours;

            // Only lit if the pipe has a load
            ItemStack load = GetLoaded(stack, world.Api);
            if (load == null || load.StackSize <= 0)
            {
                // Clear any lingering burn attributes
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                stack.Attributes.RemoveAttribute(AttrLastBurn);
                stack.Attributes.RemoveAttribute(AttrTotalLit);
                return (0, 0, false);
            }

            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);
            if (double.IsNaN(litUntil) || litUntil < 0) litUntil = 0;

            bool lit = litUntil > now;
            double remaining = lit ? litUntil - now : 0;

            double lastBurn = stack.Attributes.GetDouble(AttrLastBurn, now);
            if (double.IsNaN(lastBurn) || lastBurn < 0) lastBurn = now;

            double prevTotal = stack.Attributes.GetDouble(AttrTotalLit, 0);
            if (double.IsNaN(prevTotal) || prevTotal < 0) prevTotal = 0;

            if (lit) prevTotal += (now - lastBurn);

            return (prevTotal, remaining, lit);
        }

        public void UpdateBurnLazy(ItemStack stack, IWorldAccessor world)
        {
            if (world.Side != EnumAppSide.Server) return;

            // Only continue if pipe has a load
            ItemStack load = GetLoaded(stack, world.Api);
            if (load == null)
            {
                // Ensure no lingering burn attributes
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                stack.Attributes.RemoveAttribute(AttrLastBurn);
                stack.Attributes.RemoveAttribute(AttrTotalLit);
                return;
            }

            var (computed, _, lit) = GetBurnState(stack, world);

            stack.Attributes.SetDouble(AttrTotalLit, computed);
            stack.Attributes.SetDouble(AttrLastBurn, world.Calendar.TotalHours);

            if (!lit)
            {
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                return;
            }

            if (computed >= MaxTotalBurnHours)
            {
                SetLoaded(stack, null); // empty pipe
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                stack.Attributes.RemoveAttribute(AttrTotalLit);
            }
        }

        public void UpdateBurn(ItemStack stack, IWorldAccessor world, EntityPlayer player)
        {
            double now = world.Calendar.TotalHours;
            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);
            if (double.IsNaN(litUntil) || litUntil == 0) return;

            DebugChatState(stack, world.Api, "UpdateBurn-Start");

            if (now > litUntil)
            {
                // Pipe went out
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                if (world.Api.Side == EnumAppSide.Server)
                {
                    (player.Player as IServerPlayer)?.SendMessage(
                        GlobalConstants.GeneralChatGroup,
                        Lang.Get("pipeleaf:pipe-unlit"),
                        EnumChatType.Notification);
                }
                var wearInv = player.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                var faceSlot = wearInv?[(int)EnumCharacterDressType.Face];
                faceSlot?.MarkDirty();
                DebugChatState(stack, world.Api, "PipeOut");
                return;
            }

            double lastBurn = stack.Attributes.GetDouble(AttrLastBurn, now);
            if (double.IsNaN(lastBurn) || lastBurn < 0) lastBurn = now;

            double prevTotal = stack.Attributes.GetDouble(AttrTotalLit, 0);
            if (double.IsNaN(prevTotal) || prevTotal < 0) prevTotal = 0;

            double computed = prevTotal + (now - lastBurn);

            // Defensive clamp: never allow totalLit to decrease
            if (computed < prevTotal)
            {
                world.Logger.Warning($"WearablePipe: UpdateBurn computed totalLit decreased (prev={prevTotal} -> computed={computed}). Clamping. lastBurn={lastBurn}, now={now}");
                computed = prevTotal;
            }

            stack.Attributes.SetDouble(AttrTotalLit, computed);
            stack.Attributes.SetDouble(AttrLastBurn, now);

            //DebugChatState(stack, world.Api, "UpdateBurn-Accumulated");

            if (computed >= MaxTotalBurnHours)
            {
                if (world.Api.Side == EnumAppSide.Server)
                {
                    (player.Player as IServerPlayer)?.SendMessage(
                        GlobalConstants.GeneralChatGroup,
                        Lang.Get("pipeleaf:pipe-max-burn"),
                        EnumChatType.Notification);
                }

                // Clear everything so the pipe really empties
                SetLoaded(stack, null);
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                stack.Attributes.RemoveAttribute(AttrTotalLit);
                // stack.Attributes.RemoveAttribute(AttrNextEffectReady);

                // Mark face slot dirty to sync to client
                var wearInv = player.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                var faceSlot = wearInv?[(int)EnumCharacterDressType.Face];
                faceSlot?.MarkDirty();

                DebugChatState(stack, world.Api, "PipeEmpty");
            }
        }



        // --------- Attribute helpers ---------

        public ItemStack GetLoaded(ItemStack pipeStack, ICoreAPI api)
        {
            var tree = pipeStack.Attributes.GetTreeAttribute(AttrPipeContents);
            var load = tree?.GetItemstack(AttrLoad);

            if (load != null)
            {
                load.ResolveBlockOrItem(api.World);
                if (load.StackSize <= 0 || load.Item == null && load.Block == null)
                {
                    return null;  // treat as empty
                }
            }

            return load;
        }

        private void SetLoaded(ItemStack pipeStack, ItemStack toSet)
        {
            if (toSet == null)
            {
                // remove the entire attribute tree if present
                pipeStack.Attributes.RemoveAttribute(AttrPipeContents);
            }
            else
            {
                var tree = pipeStack.Attributes.GetOrAddTreeAttribute(AttrPipeContents);
                // Always normalize to StackSize = 1
                ItemStack clone = toSet.Clone();
                clone.StackSize = 1;
                tree.SetItemstack(AttrLoad, clone);
            }
        }

        // --------- Loading ---------

        public bool TryLoadFrom(ItemSlot source, ItemSlot pipeSlot, ICoreAPI api, out string failCode)
        {
            failCode = null;

            if (source.Empty)
            {
                failCode = "emptysource";
                return false;
            }

            if (!(source.Itemstack?.Item is SmokableItem))
            {
                failCode = "notsmokable";
                return false;
            }
            if (!(source.Itemstack.StackSize >= 3))
            {
                failCode = "notenough";
                return false;
            }

            var pipeStack = pipeSlot.Itemstack;
            var loaded_shag = GetLoaded(pipeStack, api);

            if (loaded_shag != null)
            {
                failCode = "notempty";
                return false;
            }

            var taken = source.TakeOut(3);
            source.MarkDirty();

            if (api.Side == EnumAppSide.Client)
            {
                // get the client API
                ICoreClientAPI capi = api as ICoreClientAPI;

                // find the player doing the action
                EntityPlayer eplr = capi.World.Player.Entity;

                capi.World.PlaySoundAt(
                    new AssetLocation("sounds/walk/grass1"),
                    eplr,
                    null,
                    false,
                    16f,
                    1.0f
                );
            }

            if (taken == null || taken.StackSize <= 0)
            {
                failCode = "nothingtaken";
                return false;
            }

            SetLoaded(pipeStack, taken);
            pipeStack.Attributes.SetDouble(AttrTotalLit, 0);
            pipeSlot.MarkDirty();
            return true;
        }

        // --------- Smoking ---------

        public bool TrySmoke(IWorldAccessor world, EntityPlayer player, ItemStack pipeStack, out string failCode)
        {
            failCode = null;
            if (!IsLit(pipeStack, world))
            {
                failCode = "pipenotlit";
                return false;
            }

            ItemStack shag = GetLoaded(pipeStack, world.Api);
            if (shag == null)
            {
                failCode = "pipeempty";
                return false;
            }

            // Check cooldown for effect
            double now = world.Calendar.TotalHours;
            double nextEffect = pipeStack.Attributes.GetDouble(AttrNextEffectReady, -1);
            if (double.IsNaN(nextEffect) || nextEffect < 0) nextEffect = 0;

            if (nextEffect >= 0 && now >= nextEffect)
            {
                if (shag.Item is SmokableItem smokable)
                {
                    ExtendBurn(pipeStack, world, 2 / 3); // successful puff keeps pipe alive
                    double nextEffectTime = now + effectCooldown;
                    // api.World.Logger.Notification($"TrySmoke: spawn exhale, smoke shag, reset effect to {nextEffectTime}");

                    pipeStack.Attributes.SetDouble(AttrNextEffectReady, nextEffectTime); // 2 minutes
                    SpawnExhaleParticles(world, player);
                    smokable.Smoke(world, player);
                }
            }
            UpdateBurnLazy(pipeStack, world);
            // DebugChatState(pipeStack, api, "TrySmoke");

            return true;
        }

        // --------- Particles ---------

        public void SpawnInhaleParticles(IWorldAccessor world, Entity entity)
        {
            var pos = entity.SidedPos;

            // Forward vector pointing the same way as the player's gaze
            var fwd = new Vec3f(
                (float)(-Math.Sin(pos.Yaw) * Math.Cos(pos.Pitch)),
                (float)(Math.Sin(pos.Pitch)),
                (float)(-Math.Cos(pos.Yaw) * Math.Cos(pos.Pitch))
            );

            // Mouth position
            var mouth = pos.XYZ
                .AddCopy(entity.LocalEyePos)
                .AddCopy(new Vec3d(0, -0.15, 0)) // slightly lower for mouth
                .AddCopy(fwd * 0.30f); // push forward

            var forwardPush = fwd * 1.1f;
            if (!mouth.IsFinite() || !forwardPush.IsFinite()) return; // skip

            if (!mouth.IsFinite() || !forwardPush.IsFinite()) return;

            var smokeHeld = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(50, 122, 139, 174),
                mouth, mouth,
                new Vec3f(-0.05f, 0.10f, -0.05f) + forwardPush,
                new Vec3f(0.05f, 0.15f, 0.05f),
                1.0f,
                0.0f,
                0.25f, 0.35f,
                EnumParticleModel.Quad
            );

            smokeHeld.SelfPropelled = false;
            smokeHeld.WindAffected = true;
            smokeHeld.AddPos.Set(0.05, 0.05, 0.05);
            world.SpawnParticles(smokeHeld);
        }


        public void SpawnExhaleParticles(IWorldAccessor world, Entity entity)
        {
            var pos = entity.SidedPos;

            // Forward vector pointing the same way as the player's gaze
            var fwd = new Vec3f(
                (float)(-Math.Sin(pos.Yaw) * Math.Cos(pos.Pitch)),
                (float)(Math.Sin(pos.Pitch)),
                (float)(-Math.Cos(pos.Yaw) * Math.Cos(pos.Pitch))
            );

            // Mouth position
            var mouth = pos.XYZ
                .AddCopy(entity.LocalEyePos)
                .AddCopy(new Vec3d(0, -0.15, 0)) // slightly lower for mouth
                .AddCopy(fwd * 0.30f); // push forward

            var forwardPush = fwd * 2.0f;

            if (!mouth.IsFinite() || !forwardPush.IsFinite()) return;

            var smokeExhale = new SimpleParticleProperties(
                12, 18,
                ColorUtil.ToRgba(35, 122, 139, 174),
                mouth, mouth,
                forwardPush + new Vec3f(-0.02f, 0.20f, -0.02f),
                fwd * 0.25f + new Vec3f(0.02f, 0.35f, 0.02f),
                2.0f,
                1.0f,
                1.0f, 1.8f,
                EnumParticleModel.Quad
            );

            smokeExhale.SelfPropelled = false;
            smokeExhale.WindAffected = true;
            smokeExhale.AddPos.Set(0.1, 0.05, 0.1);
            smokeExhale.GravityEffect = -0.02f;

            world.SpawnParticles(smokeExhale);
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);
            var world = entityItem.World;
            var block = world.BlockAccessor.GetBlock(entityItem.SidedPos.AsBlockPos);

            if (block.LiquidCode == "water")
            {
                var stack = entityItem.Itemstack;
                var load = GetLoaded(stack, world.Api);
                if (load != null)
                {
                    SetLoaded(stack, null);

                    stack.Attributes.RemoveAttribute(AttrLitUntil);
                    stack.Attributes.RemoveAttribute(AttrLastBurn);
                    stack.Attributes.RemoveAttribute(AttrTotalLit);
                    
                    var rot = new ItemStack(world.GetItem(new AssetLocation("game:rot")));
                    world.SpawnItemEntity(rot, entityItem.SidedPos.XYZ);
                }
            }
        }

        // --------- Tooltip ---------
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            var stack = inSlot.Itemstack;
            if (stack == null) return;

            var (totalLit, _, lit) = GetBurnState(stack, world);

            dsc.AppendLine(Lang.Get(lit ? "pipeleaf:Lit" : "pipeleaf:Unlit"));

            ItemStack shag = GetLoaded(stack, world.Api);
            if (shag != null && shag.Item is SmokableItem smokable)
            {
                int shagRemainingPercent = (int)(((MaxTotalBurnHours - totalLit) / MaxTotalBurnHours) * 100.0);
                dsc.AppendLine(Lang.Get($"Shag remaining: {shagRemainingPercent}%"));
                dsc.AppendLine("Contains: " + Lang.Get($"pipeleaf:item-{smokable.Code.Path}"));

                foreach (JsonObject effect in smokable.effects)
                {
                    string type = effect["type"].AsString();
                    double amount = effect["amount"].AsDouble(0);
                    string sign = amount > 0 ? "+" : (amount < 0 ? "-" : "");
                    dsc.AppendLine($"{sign}{type}");
                }
            }
            else
            {
                dsc.AppendLine(Lang.Get("pipeleaf:pipe-item-info-empty"));
            }

            double nextReady = stack.Attributes.GetDouble(AttrNextEffectReady, 0);
            if (double.IsNaN(nextReady) || nextReady < 0) nextReady = 0;
            double nowHrs = world.Calendar.TotalHours;


            if (nextReady > nowHrs)
            {
                double diffHrs = nextReady - nowHrs;

                double secondsLeft = diffHrs * 60;

                dsc.AppendLine(Lang.Get("pipeleaf:pipe-effect-ready-in", [(int)secondsLeft]));
            }
            else
            {
                dsc.AppendLine(Lang.Get("pipeleaf:pipe-effect-ready-now"));
            }
        }
    }
}
