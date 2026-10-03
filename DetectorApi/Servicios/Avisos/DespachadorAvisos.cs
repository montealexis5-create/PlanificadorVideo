using DetectorApi.Entidades;

namespace DetectorApi.Servicios.Avisos;

// Manda un aviso por todos los canales configurados. Si un canal falla, los demás se intentan igual.
public class DespachadorAvisos
{
    private readonly IEnumerable<IEnviadorAviso> _enviadores;
    private readonly ILogger<DespachadorAvisos> _logger;

    public DespachadorAvisos(IEnumerable<IEnviadorAviso> enviadores, ILogger<DespachadorAvisos> logger)
    {
        _enviadores = enviadores;
        _logger = logger;
    }

    // Devuelve el resultado de cada canal, por ejemplo "Email: enviado".
    public async Task<List<string>> Enviar(ConfiguracionAvisos configuracion, Aviso aviso, CancellationToken ct)
    {
        var resultados = new List<string>();
        foreach (var enviador in _enviadores.Where(e => e.EstaConfigurado(configuracion)))
        {
            try
            {
                await enviador.Enviar(configuracion, aviso, ct);
                resultados.Add($"{enviador.Nombre}: enviado");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo enviar el aviso por {Canal}", enviador.Nombre);
                resultados.Add($"{enviador.Nombre}: error ({ex.Message})");
            }
        }
        return resultados;
    }
}
