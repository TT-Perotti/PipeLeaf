using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace PipeLeaf
{
    public class WearablePipe : ItemWearable
    {
        const string AttrPipe = "pipeContents";
        const string AttrLoad = "stack";

        const string AttrLitUntil = "pipeLitUntil";
        const string AttrLastBurn = "lastBurnCheck";
        const string AttrTotalLit = "pipeTotalLit";
        const string AttrNextEffectReady = "pipeNextEffectReady";
        const double BurnIncrementHours = 1.33;
        const double MaxTotalBurnHours = 5;
        const double effectCooldown = 1;
        private string lastDebugState = null;

        public bool IsLit(ItemStack stack, IWorldAccessor world)
        {
            UpdateBurnLazy(stack, world);
            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);
            double now = world.Calendar.TotalHours;

            return litUntil > now;
        }

        private void DebugChatState(ItemStack stack, ICoreAPI api, string context)
        {
            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);
            double totalLit = stack.Attributes.GetDouble(AttrTotalLit, 0);
            double nextEffect = stack.Attributes.GetDouble(AttrNextEffectReady, -1);
            double now = api.World.Calendar.TotalHours;

            string msg = $"[PipeDebug:{context}] " +
                         $"LitUntil={litUntil}, Remaining={litUntil-now}, " +
                         $"TotalLit={totalLit}, NextEffectIn={nextEffect - now}s, Now={now}";

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

        public bool TryLight(ItemStack stack, IWorldAccessor world)
        {
            UpdateBurnLazy(stack, world);
            ItemStack shag = GetLoaded(stack, world.Api);
            if (shag == null) return false;  // must be loaded

            double now = world.Calendar.TotalHours;

            // 2 minutes of real time before going out if ignored
            stack.Attributes.SetDouble(AttrLitUntil, now + BurnIncrementHours);
            stack.Attributes.SetDouble(AttrLastBurn, now);

            // Next effect ready: only initialize once
            if (!stack.Attributes.HasAttribute(AttrNextEffectReady))
            {
                stack.Attributes.SetDouble(AttrNextEffectReady, now);
            }

            return true;
        }

        public void ExtendBurn(ItemStack stack, IWorldAccessor world, double add_time)
        {
            double now = world.Calendar.TotalHours;
            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);
            if (litUntil <= 0) return; // already out

            double newUntil = litUntil + add_time;
            // cap the possible extension to 2 minutes from now
            if (litUntil + add_time - now > 2) { newUntil = now + 2; };
            stack.Attributes.SetDouble(AttrLitUntil, newUntil);
        }
        public void UpdateBurnLazy(ItemStack stack, IWorldAccessor world)
        {
            double now = world.Calendar.TotalHours;
            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);

            if (litUntil <= 0) return;

            if (now > litUntil)
            {
                // Pipe has gone out
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                return;
            }

            double lastBurn = stack.Attributes.GetDouble(AttrLastBurn, now);
            double totalLit = stack.Attributes.GetDouble(AttrTotalLit, 0);
            totalLit += (now - lastBurn);

            stack.Attributes.SetDouble(AttrTotalLit, totalLit);
            stack.Attributes.SetDouble(AttrLastBurn, now);

            if (totalLit >= MaxTotalBurnHours)  // 20 minutes
            {
                SetLoaded(stack, null); // clear shag
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                stack.Attributes.RemoveAttribute(AttrTotalLit);
                stack.Attributes.RemoveAttribute(AttrNextEffectReady);
            }
        }

        public void UpdateBurn(ItemStack stack, IWorldAccessor world)
        {
            double now = world.Calendar.TotalHours;
            double litUntil = stack.Attributes.GetDouble(AttrLitUntil, 0);

            // DebugChatState(stack, world.Api, "UpdateBurn-Start");

            if (litUntil <= 0 || now > litUntil)
            {
                // Pipe went out
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                api.World.Logger.Notification("Looks like you pipe has gone out.");
                DebugChatState(stack, world.Api, "PipeOut");
                return;
            }

            double lastBurn = stack.Attributes.GetDouble(AttrLastBurn, now);

            // Track total accumulated lit time
            double totalLit = stack.Attributes.GetDouble(AttrTotalLit, 0);
            totalLit += (now - lastBurn);
            stack.Attributes.SetDouble(AttrTotalLit, totalLit);
            stack.Attributes.SetDouble(AttrLastBurn, now);

            DebugChatState(stack, world.Api, "UpdateBurn-Accumulated");

            // Empty after 20 minutes of real burn
            if (totalLit >= MaxTotalBurnHours)   // 20 minutes in ms
            {
                api.World.Logger.Notification("All that's left is dottle.");

                SetLoaded(stack, null); // clear shag
                stack.Attributes.RemoveAttribute(AttrLitUntil);
                stack.Attributes.RemoveAttribute(AttrNextEffectReady);
                stack.Attributes.RemoveAttribute(AttrTotalLit);

                DebugChatState(stack, world.Api, "PipeEmpty");
            }
        }


        // --------- Attribute helpers ---------

        public ItemStack GetLoaded(ItemStack pipeStack, ICoreAPI api)
        {
            var tree = pipeStack.Attributes.GetTreeAttribute(AttrPipe);
            var load = tree?.GetItemstack(AttrLoad);
            load?.ResolveBlockOrItem(api.World);
            return load;
        }

        private void SetLoaded(ItemStack pipeStack, ItemStack toSet)
        {
            var tree = pipeStack.Attributes.GetOrAddTreeAttribute(AttrPipe);

            if (toSet == null)
            {
                tree.RemoveAttribute(AttrLoad);
            }
            else
            {
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

            if (loaded_shag != null )
            {
                failCode = "notempty";
                return false;
            }

            var taken = source.TakeOut(3);
            source.MarkDirty();

            if (taken == null || taken.StackSize <= 0)
            {
                failCode = "nothingtaken";
                return false;
            }

            SetLoaded(pipeStack, source.Itemstack);
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

            ExtendBurn(pipeStack, world, 2/3); // successful puff keeps pipe alive

            // Check cooldown for effect
            double now = world.Calendar.TotalHours;
            double nextEffect = pipeStack.Attributes.GetDouble(AttrNextEffectReady, -1);

            if (nextEffect >= 0 && now >= nextEffect)
            {

                api.World.Logger.Notification($"TrySmoke: next effect ready - {nextEffect}");

                if (shag.Item is SmokableItem smokable)
                {
                    double nextEffectTime = now + effectCooldown;
                    api.World.Logger.Notification($"TrySmoke: spawn exhale, smoke shag, reset effect to {nextEffectTime}");

                    pipeStack.Attributes.SetDouble(AttrNextEffectReady, nextEffectTime); // 2 minutes
                    SpawnExhaleParticles(world, player);
                    smokable.Smoke(world, player);
                }
            }
            UpdateBurnLazy(pipeStack, world);
            DebugChatState(pipeStack, api, "TrySmoke");

            return true;
        }

        // --------- Particles ---------

        public void SpawnInhaleParticles(IWorldAccessor world, Entity entity)
        {
            var pos = entity.SidedPos;
            var fwd = new Vec3f(
                (float)(-Math.Sin(pos.Yaw) * Math.Cos(pos.Pitch)),
                (float)(-Math.Sin(pos.Pitch)),
                (float)(Math.Cos(pos.Yaw) * Math.Cos(pos.Pitch))
            );
            var mouth = pos.XYZ.AddCopy(entity.LocalEyePos).AddCopy(new Vec3d(0, -0.20, 0)).AddCopy(fwd * 0.20f);
            var forwardPush = fwd * 0.8f;

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
            var fwd = new Vec3f(
                (float)(-Math.Sin(pos.Yaw) * Math.Cos(pos.Pitch)),
                (float)(-Math.Sin(pos.Pitch)),
                (float)(Math.Cos(pos.Yaw) * Math.Cos(pos.Pitch))
            );
            var mouth = pos.XYZ.AddCopy(entity.LocalEyePos).AddCopy(new Vec3d(0, -0.20, 0)).AddCopy(fwd * 0.20f);

            var smokeExhale = new SimpleParticleProperties(
                12, 18,
                ColorUtil.ToRgba(35, 122, 139, 174),
                mouth, mouth,
                fwd * 0.10f + new Vec3f(-0.02f, 0.20f, -0.02f),
                fwd * 0.25f + new Vec3f(0.02f, 0.35f, 0.02f),
                3.5f,
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
            UpdateBurnLazy(stack, world);

            bool lit = IsLit(stack, world);
            dsc.AppendLine(Lang.Get(lit ? "pipeleaf:Lit" : "pipeleaf:Unlit"));

            ItemStack shag = GetLoaded(stack, world.Api);
            if (shag != null) {
                if (shag.Item is SmokableItem smokable)
                {
                    dsc.AppendLine("Contains: " + Lang.Get($"pipeleaf:item-{smokable.Code.Path}"));

                    foreach (JsonObject effect in smokable.effects) {
                        string type = effect["type"].AsString();
                        double amount = effect["amount"].AsDouble(0);

                        string sign = amount > 0 ? "+" : (amount < 0 ? "-" : "");

                        // If you just want the label with sign:
                        dsc.AppendLine($"{sign}{type}");
                    }
                }
            }

            double nextReady = stack.Attributes.GetDouble(AttrNextEffectReady, 0);
            double nowHrs = world.Calendar.TotalHours;

            if (nextReady > nowHrs)
            {
                double diffHrs = nextReady - nowHrs;

                double secondsLeft = diffHrs * 60; 

                dsc.AppendLine(Lang.Get("Effect ready in {0} seconds", (int)secondsLeft));
            }
            else
            {
                dsc.AppendLine(Lang.Get("Effect ready now"));
            }
        }
    }
}
