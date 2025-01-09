using Pipeleaf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PipeLeaf
{
    public class PipeLeafModSystem : ModSystem
    {
        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("SmokingItem", typeof(SmokingItem));
            api.RegisterItemClass("SmokableItem", typeof(SmokableItem));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.PlayerDeath += ResetSmokingEffectsOnDeath;
            api.World.Logger.StoryEvent("The smoke ascending...");
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
            api.Logger.Notification("Hello from template mod client side: " + Lang.Get("PipeLeaf:hello"));
        }
    }
}
