using System.Threading.Channels;

namespace DetectorApi.Servicios.Avisos;

// La API encola el aviso y responde enseguida; AvisosWorker lo manda en segundo plano.
// Así un email lento nunca demora la respuesta al planificador.
public class ColaDeAvisos
{
    private readonly Channel<Aviso> _canal = Channel.CreateBounded<Aviso>(
        new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Encolar(Aviso aviso) => _canal.Writer.TryWrite(aviso);

    public IAsyncEnumerable<Aviso> Leer(CancellationToken ct) => _canal.Reader.ReadAllAsync(ct);
}
