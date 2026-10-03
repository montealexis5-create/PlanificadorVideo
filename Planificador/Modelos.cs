namespace Planificador;

// Imagen que pasó el filtro y espera su turno para viajar a la nube.
public record CapturaPendiente(byte[] ImagenJpg, string NombreArchivo, int FramesDesdeUltimoEnvio);

// Lo que responde la API después de analizar la imagen (solo los datos que usa el planificador).
public record RespuestaNube(bool HayPersona, int CantidadPersonas, double Confianza);

public record ResultadoPlanificacion(bool DebeEnviar, bool CambioGlobalDetectado, double Ratio);
