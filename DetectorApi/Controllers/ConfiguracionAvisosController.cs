using DetectorApi.Dtos;
using DetectorApi.Seguridad;
using DetectorApi.Servicios.Avisos;
using Microsoft.AspNetCore.Mvc;

namespace DetectorApi.Controllers;

// Configuración de los avisos al dueño. Todo el controlador pide la clave de administrador.
[ApiController]
[Route("api/v1/configuracion/avisos")]
[RequiereClaveAdmin]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ConfiguracionAvisosController : ControllerBase
{
    private readonly ConfiguracionAvisosService _servicio;

    public ConfiguracionAvisosController(ConfiguracionAvisosService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ConfiguracionAvisosResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Obtener()
    {
        return Ok(await _servicio.Obtener());
    }

    [HttpPut]
    [ProducesResponseType(typeof(ConfiguracionAvisosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Guardar(ConfiguracionAvisosRequest request)
    {
        return Ok(await _servicio.Guardar(request));
    }

    // Manda un aviso de prueba ahora mismo y devuelve el resultado de cada canal.
    [HttpPost("prueba")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnviarPrueba(CancellationToken ct)
    {
        return Ok(await _servicio.EnviarPrueba(ct));
    }
}
