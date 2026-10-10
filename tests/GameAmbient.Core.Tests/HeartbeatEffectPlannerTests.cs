using GameAmbient.Core.Domain;
using GameAmbient.Core.Effects;

namespace GameAmbient.Core.Tests;

public sealed class HeartbeatEffectPlannerTests
{
    [Fact]
    public void FullHealth_IsHidden()
    {
        var plan = Plan(HudState.Safe, 1);

        Assert.False(plan.IsVisible);
    }

    [Fact]
    public void AnyVisibleDamage_StartsHeartbeatEvenWhileSafe()
    {
        var plan = Plan(HudState.Safe, .90);

        Assert.True(plan.IsVisible);
        Assert.InRange(plan.PeakOpacity, .10, .30);
    }

    [Fact]
    public void LowerHealth_BeatsFasterAndBrighter()
    {
        var high = Plan(HudState.Safe, .75);
        var medium = Plan(HudState.Warning, .25);
        var low = Plan(HudState.Critical, .05);

        Assert.True(high.CycleSeconds > medium.CycleSeconds);
        Assert.True(medium.CycleSeconds > low.CycleSeconds);
        Assert.True(high.PeakOpacity < medium.PeakOpacity);
        Assert.True(medium.PeakOpacity < low.PeakOpacity);
        Assert.True(high.GlowWidth < medium.GlowWidth);
        Assert.True(medium.GlowWidth < low.GlowWidth);
    }

    [Fact]
    public void UnknownSignal_IsAlwaysHidden()
    {
        var plan = Plan(HudState.Unknown, .10);

        Assert.False(plan.IsVisible);
    }

    private static HeartbeatEffectPlan Plan(HudState state, double health) =>
        HeartbeatEffectPlanner.Create(state, health, 1, 1, 2, 1.05);
}
