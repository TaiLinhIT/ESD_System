using System.Collections.Concurrent;
using ESD.Core;

namespace ESD.Service;

/// <summary>
/// Tracks the current <see cref="EsdState"/> of every device
/// by folding the live <see cref="EsdEvent"/> stream through the
/// <see cref="EsdStateMachine"/>.
///
/// Registered as a singleton: both the API and the desktop UI read
/// from the same in-memory picture — no extra polling of devices.
/// </summary>
public sealed class DeviceStateTracker : IDisposable
{
    private readonly ConcurrentDictionary<string, DeviceState> _states = new();
    private readonly ConcurrentDictionary<string, EsdStateMachine> _machines = new();
    private readonly IDeviceManager _devices;

    /// <summary>Raised whenever any device state changes.</summary>
    public event EventHandler<DeviceState>? StateChanged;

    public DeviceStateTracker(IDeviceManager devices, IEventManager events)
    {
        _devices = devices;
        events.EventReceived += OnEvent;
    }

    private void OnEvent(object? sender, EsdEvent evt)
    {
        // One state machine per device — keeps transition history
        var machine = _machines.GetOrAdd(evt.DeviceName, _ => new EsdStateMachine());

        var newState = machine.Transition(evt.EventType, evt.StrapStatus);

        var state = new DeviceState(
            DeviceName:    evt.DeviceName,
            DeviceAddress: evt.DeviceAddress,
            State:         newState,
            IsConnected:   IsDeviceOnline(evt.DeviceName),
            LastEvent:     evt,
            LastEventAt:   evt.Timestamp);

        _states[evt.DeviceName] = state;
        StateChanged?.Invoke(this, state);
    }

    /// <summary>Snapshot of every known device, ordered by name.</summary>
    public IReadOnlyList<DeviceState> GetStates()
        => _states.Values
            .Select(s => s with { IsConnected = IsDeviceOnline(s.DeviceName) })
            .OrderBy(s => s.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public DeviceState? Get(string deviceName)
        => _states.TryGetValue(deviceName, out var s)
            ? s with { IsConnected = IsDeviceOnline(deviceName) }
            : null;

    private bool IsDeviceOnline(string deviceName)
        => _devices.Devices.FirstOrDefault(d =>
               d.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
           ?.IsConnected ?? false;

    public void Dispose() { }
}

/// <summary>Live status of a single ESD station.</summary>
public sealed record DeviceState(
    string          DeviceName,
    byte            DeviceAddress,
    EsdState        State,
    bool            IsConnected,
    EsdEvent?       LastEvent,
    DateTime        LastEventAt);
