namespace BossMod.RealmReborn.Alliance.A33Cerberus;

static class CerberusArena
{
    // Terrain collision floor, clipped at the two arena door thresholds. Chain carriers can
    // interact from the floor inside the corner anchors; the anchors themselves are outside it.
    private static readonly PolygonCustom Surface = new([
        new(-16.61f, -238), new(-17.72f, -235.71f), new(-20, -234.61f),
        new(-20, -161.39f), new(-17.72f, -160.29f), new(-16.61f, -158),
        new(16.61f, -158), new(17.72f, -160.29f), new(20, -161.39f),
        new(20, -234.61f), new(17.72f, -235.71f), new(16.61f, -238)]);
    // Interior of the stomach collision walls at local Y=1, with2cm seam closure.
    // This separates the overlapping physical floors; ingestion/return are game teleports.
    private static readonly PolygonCustom Stomach = new([
        new(-2.880f, -178.454f), new(-0.987f, -178.295f), new(0.623f, -178.549f), new(0.704f, -178.785f),
        new(1.438f, -179.056f), new(2.413f, -179.994f), new(2.878f, -181.375f), new(2.404f, -182.305f),
        new(2.213f, -183.222f), new(2.114f, -185.516f), new(2.689f, -186.760f), new(3.584f, -187.750f),
        new(4.798f, -188.586f), new(6.156f, -189.038f), new(6.595f, -189.326f), new(7.176f, -190.044f),
        new(10.307f, -189.964f), new(11.338f, -189.275f), new(11.667f, -189.459f), new(11.931f, -190.016f),
        new(11.856f, -190.953f), new(12.486f, -194.318f), new(12.237f, -195.020f), new(12.216f, -195.731f),
        new(12.537f, -196.042f), new(12.937f, -196.899f), new(12.534f, -197.390f), new(12.487f, -198.013f),
        new(13.362f, -199.922f), new(15.581f, -203.564f), new(15.020f, -203.779f), new(15.077f, -204.398f),
        new(14.731f, -204.970f), new(12.883f, -205.237f), new(10.978f, -208.292f), new(9.518f, -209.248f),
        new(8.526f, -210.346f), new(8.302f, -211.460f), new(3.914f, -213.276f), new(3.270f, -213.066f),
        new(3.773f, -212.717f), new(4.574f, -211.490f), new(4.611f, -211.240f), new(4.242f, -210.281f),
        new(3.808f, -209.839f), new(2.691f, -209.624f), new(0.094f, -208.657f), new(-0.657f, -209.666f),
        new(-0.733f, -212.313f), new(-1.032f, -212.725f), new(-2.401f, -212.272f), new(-3.384f, -212.265f),
        new(-4.992f, -212.610f), new(-6.430f, -211.904f), new(-8.045f, -211.603f), new(-11.454f, -210.279f),
        new(-11.987f, -209.934f), new(-10.940f, -208.049f), new(-10.891f, -207.347f), new(-12.260f, -205.225f),
        new(-12.312f, -204.224f), new(-11.838f, -203.669f), new(-11.878f, -203.249f), new(-12.173f, -202.381f),
        new(-12.116f, -202.152f), new(-13.074f, -199.614f), new(-13.231f, -199.507f), new(-13.752f, -197.145f),
        new(-13.262f, -197.176f), new(-12.640f, -196.701f), new(-13.180f, -194.946f), new(-12.567f, -193.612f),
        new(-10.319f, -192.126f), new(-9.399f, -190.206f), new(-7.774f, -189.412f), new(-7.441f, -189.424f),
        new(-6.019f, -188.660f), new(-4.918f, -188.781f), new(-4.105f, -188.496f), new(-3.852f, -187.484f),
        new(-3.398f, -186.651f), new(-2.338f, -185.416f), new(-2.006f, -185.307f), new(-1.644f, -184.590f),
        new(-1.916f, -183.798f), new(-2.533f, -182.899f), new(-2.518f, -182.240f), new(-2.875f, -181.577f),
        new(-3.726f, -180.761f), new(-4.014f, -179.965f), new(-3.512f, -179.208f), new(-3.047f, -178.856f)
    ]);
    public static readonly ArenaBoundsCustom Bounds = Create();
    private static ArenaBoundsCustom Create()
    {
        var bounds = new ArenaBoundsCustom([Surface, Stomach]);
        return new([Surface, Stomach], WorldProjectionLayers: [
            new(Surface.ToPolygon(bounds.Center), 129.3f),
            new(Stomach.ToPolygon(bounds.Center), -500)]);
    }
}
