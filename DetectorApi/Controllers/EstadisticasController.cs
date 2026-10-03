using DetectorApi.Dtos;
using DetectorApi.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace DetectorApi.Controllers;

[ApiController]
[Route("api/v1/estadisticas")]
public class EstadisticasController : ControllerBase
{
    private readonly CapturaService _servicio;

    public EstadisticasController(CapturaService servicio)
    {
        _servicio = servicio;
    }

    // Cuánto filtró el planificador y cuánto descartó la IA.
    [HttpGet]
    [ProducesResponseType(typeof(EstadisticasResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Obtener()
    {
        return Ok(await _servicio.ObtenerEstadisticas());
    }
}
