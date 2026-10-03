namespace DetectorApi.Entidades;

// Una imagen que mandó el planificador y el resultado que dio la IA.
public class Captura
{
    public int Id { get; set; }
    public DateTime FechaHora { get; set; }
    public bool HayPersona { get; set; }
    public int CantidadPersonas { get; set; }
    // Confianza más alta que dio el modelo para la clase "persona" (de 0 a 1).
    public double Confianza { get; set; }
    public long MsInferencia { get; set; }
    public string NombreArchivo { get; set; } = "";
    // Cajas de las personas detectadas, guardadas como JSON.
    public string CajasJson { get; set; } = "[]";
    // Frames que analizó el planificador desde el envío anterior. Sumándolos se sabe cuántos filtró.
    public int FramesDesdeUltimoEnvio { get; set; }
}
