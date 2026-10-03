using DetectorApi.Datos;
using DetectorApi.Entidades;

namespace DetectorApi.Servicios.Avisos;

// Proceso en segundo plano que manda los avisos al dueño, respetando el tiempo mínimo entre uno y otro.
public class AvisosWorker : BackgroundService
{
    private readonly ColaDeAvisos _cola;
    private readonly DespachadorAvisos _despachador;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AvisosWorker> _logger;
    private DateTime? _ultimoAviso;

    public AvisosWorker(ColaDeAvisos cola, DespachadorAvisos despachador, IServiceScopeFactory scopes, ILogger<AvisosWorker> logger)
    {
        _cola = cola;
        _despachador = despachador;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var aviso in _cola.Leer(ct))
        {
            try
            {
                await Procesar(aviso, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Si falla un aviso (por ejemplo, la base no responde) el worker sigue funcionando para los próximos.
                _logger.LogError(ex, "Error al procesar un aviso");
            }
        }
    }

    private async Task Procesar(Aviso aviso, CancellationToken ct)
    {
        // El worker vive toda la aplicación; el DbContext es Scoped, por eso se pide uno nuevo por aviso.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuracion = await db.ConfiguracionAvisos.FindAsync(new object[] { ConfiguracionAvisos.ID_UNICO }, ct);
        if (configuracion == null || !configuracion.AvisosActivos)
            return;
        if (!CorrespondeAvisar(_ultimoAviso, aviso.FechaHora, configuracion.MinutosEntreAvisos))
            return;

        _ultimoAviso = aviso.FechaHora;
        await _despachador.Enviar(configuracion, aviso, ct);
    }

    public static bool CorrespondeAvisar(DateTime? ultimoAviso, DateTime ahora, int minutosEntreAvisos) =>
        ultimoAviso == null || ahora - ultimoAviso.Value >= TimeSpan.FromMinutes(minutosEntreAvisos);
}
