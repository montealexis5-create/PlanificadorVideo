using System.Net.Mail;
using System.Text.RegularExpressions;
using DetectorApi.Datos;
using DetectorApi.Dtos;
using DetectorApi.Entidades;

namespace DetectorApi.Servicios.Avisos;

public class ConfiguracionAvisosService
{
    private readonly AppDbContext _db;
    private readonly DespachadorAvisos _despachador;

    public ConfiguracionAvisosService(AppDbContext db, DespachadorAvisos despachador)
    {
        _db = db;
        _despachador = despachador;
    }

    public async Task<ConfiguracionAvisosResponse> Obtener() => Convertir(await BuscarOCrear());

    public async Task<ConfiguracionAvisosResponse> Guardar(ConfiguracionAvisosRequest request)
    {
        Validar(request);
        var configuracion = await BuscarOCrear();
        configuracion.AvisosActivos = request.AvisosActivos;
        configuracion.Email = Limpiar(request.Email);
        configuracion.WhatsappTelefono = Limpiar(request.WhatsappTelefono);
        if (!string.IsNullOrWhiteSpace(request.WhatsappApiKey))
            configuracion.WhatsappApiKey = request.WhatsappApiKey.Trim();
        configuracion.MinutosEntreAvisos = request.MinutosEntreAvisos;
        await _db.SaveChangesAsync();
        return Convertir(configuracion);
    }

    // Manda un aviso de prueba ahora mismo, sin esperar a que la IA detecte a alguien.
    public async Task<List<string>> EnviarPrueba(CancellationToken ct)
    {
        var configuracion = await BuscarOCrear();
        var resultados = await _despachador.Enviar(configuracion, new Aviso(DateTime.UtcNow, 1, 1, Array.Empty<byte>()), ct);
        if (resultados.Count == 0)
            throw new ArgumentException("Cargá un email o un WhatsApp (teléfono y API key) antes de probar.");
        return resultados;
    }

    public static void Validar(ConfiguracionAvisosRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Email) && !MailAddress.TryCreate(request.Email.Trim(), out _))
            throw new ArgumentException("El email no es válido.");
        if (!string.IsNullOrWhiteSpace(request.WhatsappTelefono) && !Regex.IsMatch(request.WhatsappTelefono.Trim(), @"^\+\d{8,15}$"))
            throw new ArgumentException("El teléfono tiene que tener código de país, por ejemplo +5491112345678.");
        if (request.MinutosEntreAvisos < 0 || request.MinutosEntreAvisos > 1440)
            throw new ArgumentException("Los minutos entre avisos tienen que estar entre 0 y 1440.");
    }

    private async Task<ConfiguracionAvisos> BuscarOCrear()
    {
        var configuracion = await _db.ConfiguracionAvisos.FindAsync(ConfiguracionAvisos.ID_UNICO);
        if (configuracion != null)
            return configuracion;

        configuracion = new ConfiguracionAvisos();
        _db.ConfiguracionAvisos.Add(configuracion);
        await _db.SaveChangesAsync();
        return configuracion;
    }

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static ConfiguracionAvisosResponse Convertir(ConfiguracionAvisos c) => new(
        c.AvisosActivos, c.Email, c.WhatsappTelefono, !string.IsNullOrEmpty(c.WhatsappApiKey), c.MinutosEntreAvisos);
}
