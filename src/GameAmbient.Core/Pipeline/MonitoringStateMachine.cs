namespace GameAmbient.Core.Pipeline;

public enum MonitoringStatus
{
    Idle,
    TargetSelected,
    Monitoring,
    Paused,
    TargetLost
}

public sealed class MonitoringStateMachine
{
    public MonitoringStatus Status { get; private set; } = MonitoringStatus.Idle;
    public bool HasTarget => Status is not MonitoringStatus.Idle and not MonitoringStatus.TargetLost;

    public bool SelectTarget()
    {
        if (Status == MonitoringStatus.TargetSelected) return false;
        Status = MonitoringStatus.TargetSelected;
        return true;
    }

    public bool Start()
    {
        if (Status == MonitoringStatus.Monitoring) return false;
        if (Status is not MonitoringStatus.TargetSelected and not MonitoringStatus.Paused) return false;
        Status = MonitoringStatus.Monitoring;
        return true;
    }

    public bool Pause()
    {
        if (Status != MonitoringStatus.Monitoring) return false;
        Status = MonitoringStatus.Paused;
        return true;
    }

    public bool Resume() => Start();

    public bool Stop()
    {
        if (Status is MonitoringStatus.Idle or MonitoringStatus.TargetSelected) return false;
        Status = HasTarget ? MonitoringStatus.TargetSelected : MonitoringStatus.Idle;
        return true;
    }

    public bool LoseTarget()
    {
        if (Status == MonitoringStatus.TargetLost) return false;
        Status = MonitoringStatus.TargetLost;
        return true;
    }

    public void ClearTarget() => Status = MonitoringStatus.Idle;
}
