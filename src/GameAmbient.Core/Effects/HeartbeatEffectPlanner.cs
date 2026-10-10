using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Effects;

public sealed record HeartbeatEffectPlan(
    bool IsVisible,
    double CycleSeconds,
    double PeakOpacity,
    double GlowWidth,
    double Danger);

public static class HeartbeatEffectPlanner
{
    public static HeartbeatEffectPlan Create(
        HudState state,
        double health,
        double intensity,
        double widthScale,
        double warningCycleSeconds,
        double criticalCycleSeconds)
    {
        if (state == HudState.Unknown || !double.IsFinite(health)) return Hidden();

        health = Math.Clamp(health, 0, 1);
        if (health >= .995) return Hidden();

        intensity = Math.Clamp(intensity, .1, 2);
        widthScale = Math.Clamp(widthScale, .25, 2.5);
        var slowCycle = Math.Clamp(warningCycleSeconds, 1.2, 4);
        var fastestCycle = Math.Clamp(criticalCycleSeconds * .55, .42, .85);
        var danger = 1 - health;

        // A non-linear curve keeps early damage calm and becomes urgent near zero health.
        var cycle = fastestCycle + ((slowCycle - fastestCycle) * Math.Pow(health, .72));
        var opacity = Math.Clamp((.09 + (.53 * Math.Pow(danger, .72))) * intensity, .04, .78);
        var width = (115 + (95 * Math.Pow(danger, .70))) * widthScale;
        return new HeartbeatEffectPlan(true, cycle, opacity, width, danger);
    }

    private static HeartbeatEffectPlan Hidden() => new(false, 0, 0, 0, 0);
}
