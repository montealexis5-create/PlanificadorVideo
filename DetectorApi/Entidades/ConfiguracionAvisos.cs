namespace DetectorApi.Entidades;

// A quién avisar cuando la IA confirma una persona. Hay una sola fila (Id = 1) y la carga el dueño desde el panel.
public class ConfiguracionAvisos
{
    public const int ID_UNICO = 1;

    public int Id { get; set; } = ID_UNICO;
    public bool AvisosActivos { get; set; }
    public string? Email { get; set; }
    public string? WhatsappTelefono { get; set; }
    // Clave personal que da CallMeBot (servicio gratuito para mandarse mensajes de WhatsApp a uno mismo).
    public string? WhatsappApiKey { get; set; }
    // Tiempo mínimo entre dos avisos, para no mandar un mensaje por cada foto de la misma persona.
    public int MinutosEntreAvisos { get; set; } = 1;
}
