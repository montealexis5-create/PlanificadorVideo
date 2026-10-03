using DetectorApi.Seguridad;
using DetectorApi.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace DetectorApi.Controllers;

[ApiController]
[Route("api/v1/camaras")]
public class CamarasController : ControllerBase
{
    private readonly EnlaceCamaraService _servicio;

    public CamarasController(EnlaceCamaraService servicio)
    {
        _servicio = servicio;
    }

    // Link y QR para conectar un celular como cámara. Pide la clave de administrador porque el link lleva la API key.
    [HttpGet("enlace")]
    [RequiereClaveAdmin]
    [ProducesResponseType(typeof(EnlaceCamara), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult ObtenerEnlace()
    {
        var enlace = _servicio.Generar();
        return enlace == null
            ? Conflict("No se encontró la IP de la compu en la red. Configurala en CamaraRemota:Host.")
            : Ok(enlace);
    }
}
