using GameAmbient.Core.Pipeline;

namespace GameAmbient.Core.Tests;

public sealed class MonitoringStateMachineTests
{
    [Fact]
    public void Start_and_stop_are_idempotent()
    {
        var state = new MonitoringStateMachine();
        Assert.False(state.Start());
        Assert.True(state.SelectTarget());
        Assert.True(state.Start());
        Assert.False(state.Start());
        Assert.True(state.Stop());
        Assert.False(state.Stop());
        Assert.Equal(MonitoringStatus.TargetSelected, state.Status);
    }

    [Fact]
    public void Focus_pause_can_resume()
    {
        var state = new MonitoringStateMachine();
        state.SelectTarget();
        state.Start();
        Assert.True(state.Pause());
        Assert.Equal(MonitoringStatus.Paused, state.Status);
        Assert.True(state.Resume());
        Assert.Equal(MonitoringStatus.Monitoring, state.Status);
    }

    [Fact]
    public void Target_loss_is_safe_and_idempotent()
    {
        var state = new MonitoringStateMachine();
        state.SelectTarget();
        state.Start();
        Assert.True(state.LoseTarget());
        Assert.False(state.LoseTarget());
        Assert.False(state.Start());
        Assert.Equal(MonitoringStatus.TargetLost, state.Status);
    }
}
