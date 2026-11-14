using Microsoft.AspNetCore.Mvc;
using TriasDeparture.Application.Services;
using TriasDeparture.Domain.Models;

namespace TriasDeparture.Api.Controllers;

/// <summary>Departure endpoints for a specific stop.</summary>
[ApiController]
[Route("api/departures")]
[Produces("application/json")]
public sealed class DeparturesController : ControllerBase
{
    private readonly DepartureService _service;

    public DeparturesController(DepartureService service) => _service = service;

    /// <summary>Returns upcoming departures for the given stop, optionally filtered.</summary>
    /// <param name="stopId">Stop ID (StopPointRef), e.g. at:46:3171</param>
    /// <param name="line">Only this line, e.g. 3 or N5</param>
    /// <param name="direction">Only this direction of the line: outward or inward</param>
    /// <param name="limit">Maximum number of departures (1–100)</param>
    [HttpGet("{stopId}")]
    [ProducesResponseType(typeof(IReadOnlyList<DepartureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDepartures(
        string stopId,
        [FromQuery] string? line,
        [FromQuery] string? direction,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stopId))
            return BadRequest("stopId is required.");
        if (direction is not null && direction.ToLowerInvariant() is not ("outward" or "inward"))
            return BadRequest("direction must be 'outward' or 'inward'.");
        if (limit is < 1 or > 100)
            return BadRequest("limit must be between 1 and 100.");

        var departures = await _service.GetDeparturesAsync(stopId, new DepartureFilter(line, direction, limit), ct);
        return Ok(departures);
    }

    /// <summary>Returns upcoming departures filtered by line number.</summary>
    /// <param name="stopId">Stop ID (StopPointRef)</param>
    /// <param name="line">Line number, e.g. 3</param>
    [HttpGet("{stopId}/line/{line}")]
    [ProducesResponseType(typeof(IReadOnlyList<DepartureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDeparturesByLine(string stopId, string line, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stopId) || string.IsNullOrWhiteSpace(line))
            return BadRequest("stopId and line are required.");

        var departures = await _service.GetDeparturesByLineAsync(stopId, line, ct);
        return Ok(departures);
    }
}
