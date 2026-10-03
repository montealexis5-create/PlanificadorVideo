using DetectorApi.Entidades;

namespace DetectorApi.Servicios.Avisos;

// Manda el aviso por WhatsApp con CallMeBot, un servicio gratuito para mandarse mensajes a uno mismo.
// El dueño consigue su API key una sola vez siguiendo los pasos de https://www.callmebot.com/blog/free-api-whatsapp-messages/
public class EnviadorWhatsapp : IEnviadorAviso
{
    private const string URL_CALLMEBOT = "https://api.callmebot.com/whatsapp.php";

    private readonly IHttpClientFactory _clientes;

    public EnviadorWhatsapp(IHttpClientFactory clientes)
    {
        _clientes = clientes;
    }

    public string Nombre => "WhatsApp";

    public bool EstaConfigurado(ConfiguracionAvisos configuracion) =>
        !string.IsNullOrWhiteSpace(configuracion.WhatsappTelefono) && !string.IsNullOrWhiteSpace(configuracion.WhatsappApiKey);

    public async Task Enviar(ConfiguracionAvisos configuracion, Aviso aviso, CancellationToken ct)
    {
        string url = $"{URL_CALLMEBOT}?phone={Uri.EscapeDataString(configuracion.WhatsappTelefono!)}" +
                     $"&text={Uri.EscapeDataString(aviso.Texto)}&apikey={Uri.EscapeDataString(configuracion.WhatsappApiKey!)}";
        using var respuesta = await _clientes.CreateClient().GetAsync(url, ct);
        respuesta.EnsureSuccessStatusCode();
    }
}
