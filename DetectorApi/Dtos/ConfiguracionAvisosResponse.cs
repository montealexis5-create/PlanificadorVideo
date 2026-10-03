namespace DetectorApi.Dtos;

// La API key de WhatsApp nunca se devuelve: solo se informa si está cargada.
public record ConfiguracionAvisosResponse(
    bool AvisosActivos,
    string? Email,
    string? WhatsappTelefono,
    bool WhatsappApiKeyCargada,
    int MinutosEntreAvisos);
