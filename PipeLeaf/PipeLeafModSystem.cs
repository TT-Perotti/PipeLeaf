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
            api.Event.PlayerNowPlaying += (IServerPlayer iServerPlayer) =>
            {
                if (iServerPlayer.Entity is EntityPlayer)
                {
                    Entity entity = iServerPlayer.Entity;
                    entity.AddBehavior(new TempEffectFixBehavior(entity));

                    //api.Logger.Debug("[Potion] Adding PotionFixBehavior to spawned EntityPlayer");
                    TempEffect tempEffect = new();
                    EntityPlayer player = (iServerPlayer.Entity as EntityPlayer);
                    tempEffect.ResetAllTempStats(player, "pipeleafmod");
                    //api.Logger.Debug("potion player ready");
                }
            };
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Logger.Notification("Hello from template mod client side: " + Lang.Get("PipeLeaf:hello"));
        }
    }
}
