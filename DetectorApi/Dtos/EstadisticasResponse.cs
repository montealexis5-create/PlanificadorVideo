namespace DetectorApi.Dtos;

public record EstadisticasResponse(
    long FramesAnalizados,
    int ImagenesRecibidas,
    int PersonasConfirmadas,
    int DescartadasPorIa,
    double PorcentajeFiltradoLocal,   // Frames que el planificador descartó sin mandarlos a la nube
    double PorcentajeDescartadoIa,    // Imágenes recibidas en las que la IA no encontró personas
    double PromedioMsInferencia);
