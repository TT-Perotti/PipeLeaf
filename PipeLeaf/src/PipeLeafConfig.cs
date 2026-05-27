using Vintagestory.API.Common;

namespace PipeLeaf
{
    public class PipeleafConfig
    {
        /// <summary>
        /// How many in-game hours are added when the pipe is lit.
        /// Must be greater than 0.
        /// Setting this to 0 or a negative number will cause the pipe to immediately go out.
        /// </summary>
        public double BurnIncrementHours = 2.0;

        /// <summary>
        /// Maximum total in-game hours the pipe can burn before it empties.
        /// Must be greater than BurnIncrementHours and greater than 0.
        /// Setting this to 0 or a negative number will prevent the pipe from being usable.
        /// </summary>
        public double MaxTotalBurnHours = 6.0;

        /// <summary>
        /// Cooldown between smoke effects, in in-game hours.
        /// Must be greater than 0.
        /// Setting this too low may cause effects to trigger extremely frequently.
        /// </summary>
        public double EffectCooldown = 1.25;

        public void Validate(ICoreAPI api)
        {
            bool changed = false;

            BurnIncrementHours = Sanitize(
                BurnIncrementHours, 0.01, 24, 2.0, ref changed
            );

            MaxTotalBurnHours = Sanitize(
                MaxTotalBurnHours, BurnIncrementHours, 168, 6.0, ref changed
            );

            EffectCooldown = Sanitize(
                EffectCooldown, 0.01, 24, 1.25, ref changed
            );

            if (changed)
            {
                api.Logger.Warning("[Pipeleaf] Invalid config values detected and corrected.");
                api.StoreModConfig(this, "pipeleaf.json");
            }
        }

        private double Sanitize(
            double value,
            double min,
            double max,
            double fallback,
            ref bool changed
        )
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                changed = true;
                return fallback;
            }

            if (value < min)
            {
                changed = true;
                return min;
            }

            if (value > max)
            {
                changed = true;
                return max;
            }

            return value;
        }

    }

}