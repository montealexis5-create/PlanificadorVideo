using System.Diagnostics;
using System.Text.Json;
using DetectorApi.Datos;
using DetectorApi.Dtos;
using DetectorApi.Entidades;
using DetectorApi.Hubs;
using DetectorApi.Servicios.Avisos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace DetectorApi.Servicios;

public class CapturaService
{
    private const long TAMANIO_MAXIMO = 5 * 1024 * 1024;
    private const int LADO_MAXIMO = 4096;   // Evita imágenes enormes que, al descomprimirse, ocupen gigas de memoria

    private readonly AppDbContext _db;
    private readonly IDetectorPersonas _detector;
    private readonly IHubContext<CapturasHub> _hub;
    private readonly ColaDeAvisos _avisos;
    private readonly ILogger<CapturaService> _logger;
    private readonly string _carpetaImagenes;

    public CapturaService(AppDbContext db, IDetectorPersonas detector, IHubContext<CapturasHub> hub,
        ColaDeAvisos avisos, ILogger<CapturaService> logger, IConfiguration configuracion)
    {
        _db = db;
        _detector = detector;
        _hub = hub;
        _avisos = avisos;
        _logger = logger;
        _carpetaImagenes = configuracion["Deteccion:CarpetaImagenes"]!;
    }

    public async Task<CapturaResponse> Registrar(IFormFile? imagen, int framesDesdeUltimoEnvio)
    {
        if (imagen == null || imagen.Length == 0)
            throw new ArgumentException("Falta la imagen.");
        if (imagen.Length > TAMANIO_MAXIMO)
            throw new ArgumentException("La imagen no puede pesar más de 5 MB.");
        if (framesDesdeUltimoEnvio < 0)
            throw new ArgumentException("FramesDesdeUltimoEnvio no puede ser negativo.");

        using var memoria = new MemoryStream();
        await imagen.CopyToAsync(memoria);
        byte[] bytes = memoria.ToArray();

        var reloj = Stopwatch.StartNew();
        ResultadoDeteccion resultado;
        try
        {
            ValidarTamanio(Image.Identify(bytes));   // Lee solo el encabezado, sin descomprimir la imagen
            resultado = _detector.Detectar(bytes);
        }
        catch (ImageFormatException)
        {
            throw new ArgumentException("El archivo no es una imagen válida.");
        }
        reloj.Stop();

        string nombreArchivo = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.jpg";
        Directory.CreateDirectory(_carpetaImagenes);
        await File.WriteAllBytesAsync(Path.Combine(_carpetaImagenes, nombreArchivo), bytes);

        var captura = new Captura
        {
            FechaHora = DateTime.UtcNow,
            HayPersona = resultado.Personas.Count > 0,
            CantidadPersonas = resultado.Personas.Count,
            Confianza = resultado.ConfianzaMaxima,
            MsInferencia = reloj.ElapsedMilliseconds,
            NombreArchivo = nombreArchivo,
            CajasJson = JsonSerializer.Serialize(resultado.Personas),
            FramesDesdeUltimoEnvio = framesDesdeUltimoEnvio
        };
        _db.Capturas.Add(captura);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Captura {Id}: {Personas} persona(s), IA en {Ms} ms", captura.Id, captura.CantidadPersonas, captura.MsInferencia);
        var respuesta = Convertir(captura);
        await _hub.Clients.All.SendAsync("NuevaCaptura", respuesta);   // Aviso en vivo a los paneles abiertos
        if (captura.HayPersona)
            _avisos.Encolar(new Aviso(captura.FechaHora, captura.CantidadPersonas, captura.Confianza, bytes));
        return respuesta;
    }

    // Las más nuevas primero. hayPersona: null = todas, true = solo personas, false = solo descartadas por la IA.
    public async Task<List<CapturaResponse>> Listar(bool? hayPersona, int limite)
    {
        if (limite < 1 || limite > 200)
            throw new ArgumentException("El límite tiene que estar entre 1 y 200.");

        var capturas = await _db.Capturas
            .Where(c => hayPersona == null || c.HayPersona == hayPersona)
            .OrderByDescending(c => c.FechaHora)
            .Take(limite)
            .ToListAsync();
        return capturas.Select(Convertir).ToList();
    }

    // Ruta del archivo de la imagen, o null si la captura no existe.
    public async Task<string?> ObtenerRutaImagen(int id)
    {
        var captura = await _db.Capturas.FindAsync(id);
        if (captura == null)
            return null;
        string ruta = Path.GetFullPath(Path.Combine(_carpetaImagenes, captura.NombreArchivo));
        return File.Exists(ruta) ? ruta : null;
    }

    public async Task<EstadisticasResponse> ObtenerEstadisticas()
    {
        int recibidas = await _db.Capturas.CountAsync();
        int personas = await _db.Capturas.CountAsync(c => c.HayPersona);
        long frames = await _db.Capturas.SumAsync(c => (long)c.FramesDesdeUltimoEnvio);
        double promedioMs = recibidas > 0 ? await _db.Capturas.AverageAsync(c => c.MsInferencia) : 0;

        return new EstadisticasResponse(
            frames,
            recibidas,
            personas,
            recibidas - personas,
            frames > 0 ? 1 - (double)recibidas / frames : 0,
            recibidas > 0 ? (double)(recibidas - personas) / recibidas : 0,
            promedioMs);
    }

    private static void ValidarTamanio(ImageInfo info)
    {
        if (info.Width > LADO_MAXIMO || info.Height > LADO_MAXIMO)
            throw new ArgumentException($"La imagen no puede medir más de {LADO_MAXIMO} píxeles de lado.");
    }

    private static CapturaResponse Convertir(Captura c) => new(
        c.Id,
        c.FechaHora,
        c.HayPersona,
        c.CantidadPersonas,
        c.Confianza,
        c.MsInferencia,
        $"/api/v1/capturas/{c.Id}/imagen",
        JsonSerializer.Deserialize<List<CajaDto>>(c.CajasJson) ?? new List<CajaDto>());
}
