using DetectorApi.Dtos;
using DetectorApi.Seguridad;
using DetectorApi.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DetectorApi.Controllers;

[ApiController]
[Route("api/v1/capturas")]
public class CapturasController : ControllerBase
{
    private const long TAMANIO_MAXIMO_PEDIDO = 6 * 1024 * 1024;

    private readonly CapturaService _servicio;

    public CapturasController(CapturaService servicio)
    {
        _servicio = servicio;
    }

    // Recibe una imagen del planificador o de una cámara, la analiza con la IA y guarda el resultado.
    [HttpPost]
    [RequiereApiKey]
    [EnableRateLimiting("capturas")]
    [RequestSizeLimit(TAMANIO_MAXIMO_PEDIDO)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CapturaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Registrar([FromForm] CapturaRequest request)
    {
        var respuesta = await _servicio.Registrar(request.Imagen, request.FramesDesdeUltimoEnvio);
        return StatusCode(StatusCodes.Status201Created, respuesta);
    }

    // hayPersona: sin valor = todas, true = solo personas, false = solo las descartadas por la IA.
    [HttpGet]
    [ProducesResponseType(typeof(List<CapturaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar([FromQuery] bool? hayPersona, [FromQuery] int limite = 50)
    {
        return Ok(await _servicio.Listar(hayPersona, limite));
    }

    [HttpGet("{id}/imagen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerImagen(int id)
    {
        string? ruta = await _servicio.ObtenerRutaImagen(id);
        return ruta == null ? NotFound("No existe la captura o su imagen.") : PhysicalFile(ruta, "image/jpeg");
    }
}
