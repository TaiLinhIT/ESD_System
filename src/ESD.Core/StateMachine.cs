namespace ESD.Core;

public enum EsdState
{
    Disconnected,
    Idle,
    WorkerPresent,
    StrapOk,
    StrapNg,
    Error,
}

/// <summary>
/// Per-device state machine driven by decoded protocol events.
/// This is pure domain logic — no serial/protocol knowledge here.
/// </summary>
public sealed class EsdStateMachine
{
    public EsdState State { get; private set; } = EsdState.Disconnected;

    public EsdState Transition(EsdEventType eventType, WristStrapStatus strapStatus)
    {
        State = eventType switch
        {
            EsdEventType.DeviceConnected        => EsdState.Idle,
            EsdEventType.DeviceDisconnected     => EsdState.Disconnected,

            EsdEventType.WorkerDetected         => strapStatus switch
            {
                WristStrapStatus.Ok      => EsdState.StrapOk,
                WristStrapStatus.Ng      => EsdState.StrapNg,
                WristStrapStatus.Warning => EsdState.StrapNg,
                _                        => EsdState.WorkerPresent,
            },

            EsdEventType.WorkerRemoved          => EsdState.Idle,

            EsdEventType.WristStrapConnected    => EsdState.StrapOk,
            EsdEventType.WristStrapDisconnected => EsdState.StrapNg,

            EsdEventType.EsdTestPass            => EsdState.StrapOk,
            EsdEventType.EsdTestFail            => EsdState.StrapNg,

            EsdEventType.Alarm                  => EsdState.Error,
            EsdEventType.AlarmReset             => EsdState.Idle,

            _ => State,
        };

        return State;
    }
}
