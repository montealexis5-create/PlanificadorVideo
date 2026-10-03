namespace DetectorApi.Dtos;

// Lo que manda el planificador (multipart/form-data). Es clase y no record porque [FromForm] la completa por propiedades.
public class CapturaRequest
{
    public IFormFile? Imagen { get; set; }
    public int FramesDesdeUltimoEnvio { get; set; }
}
