using DetectorApi.Datos;
using DetectorApi.Hubs;
using DetectorApi.Seguridad;
using DetectorApi.Servicios;
using DetectorApi.Servicios.Avisos;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// EnableRetryOnFailure: si la base todavía está arrancando, reintenta la conexión en vez de fallar.
builder.Services.AddDbContext<AppDbContext>(opciones =>
    opciones.UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql.EnableRetryOnFailure()));

// Singleton: el modelo de IA se carga en memoria una sola vez al arrancar y lo comparten todas las peticiones.
builder.Services.AddSingleton<IDetectorPersonas, DetectorPersonas>();
builder.Services.AddScoped<CapturaService>();
builder.Services.AddSingleton<EnlaceCamaraService>();

// Avisos al dueño (email y WhatsApp), enviados en segundo plano.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEnviadorAviso, EnviadorEmail>();
builder.Services.AddSingleton<IEnviadorAviso, EnviadorWhatsapp>();
builder.Services.AddSingleton<DespachadorAvisos>();
builder.Services.AddSingleton<ColaDeAvisos>();
builder.Services.AddHostedService<AvisosWorker>();
builder.Services.AddScoped<ConfiguracionAvisosService>();

// Límite de pedidos: como mucho 5 imágenes por segundo, para que nadie sature el modelo de IA.
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddFixedWindowLimiter("capturas", limite =>
    {
        limite.PermitLimit = 5;
        limite.Window = TimeSpan.FromSeconds(1);
    });
});

builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddExceptionHandler<ManejadorDeErrores>();
builder.Services.AddProblemDetails();

// Botón "Authorize" de Swagger para cargar las claves y probar los endpoints protegidos.
builder.Services.AddSwaggerGen(opciones =>
{
    foreach (string header in new[] { RequiereApiKeyAttribute.HEADER, RequiereClaveAdminAttribute.HEADER })
    {
        opciones.AddSecurityDefinition(header, new OpenApiSecurityScheme
        {
            Name = header,
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header
        });
        opciones.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference(header, documento), new List<string>() }
        });
    }
});

var app = builder.Build();

app.UseExceptionHandler();

// Crea la base y la tabla si todavía no existen.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

// Encabezados de seguridad: evitan que el navegador adivine tipos de archivo, que el panel se meta en un
// iframe de otro sitio y que se ejecuten scripts que no sean los del panel o el cliente de SignalR.
app.Use(async (contexto, siguiente) =>
{
    var cabeceras = contexto.Response.Headers;
    cabeceras["X-Content-Type-Options"] = "nosniff";
    cabeceras["X-Frame-Options"] = "DENY";
    cabeceras["Referrer-Policy"] = "no-referrer";
    cabeceras["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
    if (!contexto.Request.Path.StartsWithSegments("/swagger"))   // Swagger usa scripts propios
    {
        cabeceras["Content-Security-Policy"] = "default-src 'self'; script-src 'self' https://cdnjs.cloudflare.com; " +
            "style-src 'self'; img-src 'self' data:; connect-src 'self' ws: wss:; frame-ancestors 'none'";
    }
    await siguiente();
});

app.UseSwagger();
app.UseSwaggerUI();

// Sirve el panel web que está en la carpeta wwwroot (index.html).
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<CapturasHub>("/hubs/capturas");

app.Run();
