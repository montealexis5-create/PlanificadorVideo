namespace DetectorApi.Dtos;

// Caja de una persona detectada. Las coordenadas van de 0 a 1, relativas al tamaño de la imagen.
public record CajaDto(double X, double Y, double Ancho, double Alto, double Confianza);

public record CapturaResponse(
    int Id,
    DateTime FechaHora,
    bool HayPersona,
    int CantidadPersonas,
    double Confianza,
    long MsInferencia,
    string UrlImagen,
    List<CajaDto> Cajas);
