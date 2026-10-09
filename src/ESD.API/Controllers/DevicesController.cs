using ESD.API.Dto;
using ESD.Core;
using ESD.Service;
using Microsoft.AspNetCore.Mvc;

namespace ESD.API.Controllers;

/// <summary>Device registry + live per-device state.</summary>
[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly EsdRuntime          _runtime;
    private readonly DeviceStateTracker  _tracker;

    public DevicesController(EsdRuntime runtime, DeviceStateTracker tracker)
    {
        _runtime = runtime;
        _tracker = tracker;
    }

    /// <summary>All configured devices with their live state.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceDto>), StatusCodes.Status200OK)]
    public IActionResult GetDevices()
        => Ok(_runtime.Devices.Devices.Select(Map).ToList());

    /// <summary>Single device detail by name.</summary>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetDevice(string name)
    {
        var device = _runtime.Devices.Devices
            .FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (device is null)
            return NotFound(new { error = $"Device '{name}' not found." });

        return Ok(Map(device));
    }

    private DeviceDto Map(IEsdDevice device)
    {
        var state = _tracker.Get(device.Name);
        var cfg   = _runtime.Settings.Devices
            .FirstOrDefault(d => d.Name.Equals(device.Name, StringComparison.OrdinalIgnoreCase));

        return new DeviceDto(
            Name:             device.Name,
            Address:          device.Address,
            Protocol:         cfg?.ProtocolVersion ?? "unknown",
            PortName:         cfg?.PortName ?? string.Empty,
            IsConnected:      device.IsConnected,
            IsOnline:         state?.IsConnected ?? false,
            State:            state?.State.ToString() ?? "Unknown",
            LastEventType:    state?.LastEvent?.EventType.ToString(),
            LastEmployeeId:   state?.LastEvent?.EmployeeId,
            LastStrapStatus:  state?.LastEvent?.StrapStatus.ToString(),
            LastEventAt:      state?.LastEventAt);
    }
}
