using Vintagestory.API.MathTools;

public static class VecExtensions
{
    public static bool IsFinite(this Vec3f v)
    {
        return !(float.IsNaN(v.X) || float.IsInfinity(v.X)
              || float.IsNaN(v.Y) || float.IsInfinity(v.Y)
              || float.IsNaN(v.Z) || float.IsInfinity(v.Z));
    }

    public static bool IsFinite(this Vec3d v)
    {
        return !(double.IsNaN(v.X) || double.IsInfinity(v.X)
              || double.IsNaN(v.Y) || double.IsInfinity(v.Y)
              || double.IsNaN(v.Z) || double.IsInfinity(v.Z));
    }
}