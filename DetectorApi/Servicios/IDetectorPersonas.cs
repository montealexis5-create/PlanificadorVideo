using DetectorApi.Dtos;

namespace DetectorApi.Servicios;

public record ResultadoDeteccion(List<CajaDto> Personas, double ConfianzaMaxima);

// Con una interfaz, CapturaService no depende del modelo real y se puede probar con un detector falso.
public interface IDetectorPersonas
{
    ResultadoDeteccion Detectar(byte[] imagen);
}
