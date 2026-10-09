using ESD.API.Dto;
using ESD.Core;
using ESD.Service;
using Microsoft.AspNetCore.Mvc;

namespace ESD.API.Controllers;

/// <summary>Health and system-wide status.</summary>
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly EsdRuntime _runtime;
    private readonly DeviceStateTracker _tracker;

    public StatusController(EsdRuntime runtime, DeviceStateTracker tracker)
    {
        _runtime  = runtime;
        _tracker  = tracker;
    }

    /// <summary>Overall service status + device counters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SystemStatusDto), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        var states  = _tracker.GetStates();
        var devices = _runtime.Devices.Devices;

        var status = new SystemStatusDto(
            Service:          "ESD.API",
            Status:           "running",
            TotalDevices:     devices.Count,
            ConnectedDevices: devices.Count(d => d.IsConnected),
            OnlineDevices:    states.Count(s => s.IsConnected),
            DbQueueLength:    _runtime.DbWorker.QueueCount,
            ServerTimeUtc:    DateTime.UtcNow);

        return Ok(status);
    }

    /// <summary>Lightweight liveness probe for load balancers.</summary>
    [HttpGet("~/api/health")]
    [ProducesResponseType(typeof(HealthDto), StatusCodes.Status200OK)]
    public IActionResult Health()
        => Ok(new HealthDto("healthy", "1.0", DateTime.UtcNow));
}
