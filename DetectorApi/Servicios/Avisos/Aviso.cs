namespace DetectorApi.Servicios.Avisos;

public record Aviso(DateTime FechaHora, int CantidadPersonas, double Confianza, byte[] Imagen)
{
    public string Texto =>
        $"Alerta: se detectaron {CantidadPersonas} persona(s) el {FechaHora.ToLocalTime():dd/MM/yyyy} a las {FechaHora.ToLocalTime():HH:mm:ss} " +
        $"(confianza {Confianza:P0}).";
}
