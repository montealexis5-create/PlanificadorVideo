using Microsoft.AspNetCore.SignalR;

namespace DetectorApi.Hubs;

// El panel web se conecta acá para recibir cada captura nueva en vivo, sin recargar la página.
// No tiene métodos: el servidor es el único que envía mensajes ("NuevaCaptura").
public class CapturasHub : Hub
{
}
