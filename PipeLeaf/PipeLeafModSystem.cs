using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
            api.Logger.Notification("Hello from template mod: " + api.Side);
            base.Start(api);
            api.RegisterItemClass("SmokingItem", typeof(SmokingItem));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Logger.Notification("Hello from template mod server side: " + Lang.Get("PipeLeaf:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Logger.Notification("Hello from template mod client side: " + Lang.Get("PipeLeaf:hello"));
        }
    }
}
