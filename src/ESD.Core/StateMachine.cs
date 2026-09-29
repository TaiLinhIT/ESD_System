namespace ESD.Core;

public enum EsdState { Disconnected, Idle, WaitingForDevice, UserDetected, Error }

public sealed class EsdStateMachine
{
    public EsdState State { get; private set; } = EsdState.Disconnected;

    public EsdState Process(DeviceMessage message)
    {
        if (message.Data.Length == 0)
            return State = EsdState.Error;

        // Demo protocol:
        // "CONNECT" -> idle
        // "REMOVE:<employee>" -> employee removed
        // "INSTALL:<employee>" -> employee installed
        // Other data -> waiting/device event
        var text = message.RawText.Trim();

        if (text.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
            State = EsdState.Idle;
        else if (text.StartsWith("REMOVE:", StringComparison.OrdinalIgnoreCase) ||
                 text.StartsWith("INSTALL:", StringComparison.OrdinalIgnoreCase))
            State = EsdState.UserDetected;
        else
            State = EsdState.WaitingForDevice;

        return State;
    }
}
