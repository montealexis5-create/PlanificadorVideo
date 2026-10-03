namespace DetectorApi.Dtos;

// Si WhatsappApiKey llega vacía se conserva la que ya estaba guardada.
public record ConfiguracionAvisosRequest(
    bool AvisosActivos,
    string? Email,
    string? WhatsappTelefono,
    string? WhatsappApiKey,
    int MinutosEntreAvisos);
