using Pipeleaf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Pipeleaf
{
    public class TempEffectFixBehavior : EntityBehavior
    {

        public TempEffectFixBehavior(Entity entity) : base(entity)
        {

        }

        private IServerPlayer GetIServerPlayer()
        {
            return this.entity.World.PlayerByUid((this.entity as EntityPlayer).PlayerUID) as IServerPlayer;
        }

        /* This override is to add the behavior to the player of when they die they also reset all of their potion effects */
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            IServerPlayer player = GetIServerPlayer();

            TempEffect tempEffect = new();
            tempEffect.ResetAllTempStats((player.Entity as EntityPlayer), "pipeleafmod");
            tempEffect.ResetAllListeners((player.Entity as EntityPlayer), "pipeleafmod");

            base.OnEntityDeath(damageSourceForDeath);
        }

        public override string PropertyName()
        {
            return "TempEffectFixBehavior";
        }
    }
}