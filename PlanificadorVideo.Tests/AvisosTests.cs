using DetectorApi.Dtos;
using DetectorApi.Entidades;
using DetectorApi.Seguridad;
using DetectorApi.Servicios.Avisos;
using Microsoft.Extensions.Logging.Abstractions;

namespace PlanificadorVideo.Tests;

public class AvisosTests
{
    private static readonly DateTime Ahora = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Aviso AvisoDePrueba = new(Ahora, 1, 0.9, Array.Empty<byte>());

    [Fact]
    public void CorrespondeAvisar_PrimerAviso_Si()
    {
        Assert.True(AvisosWorker.CorrespondeAvisar(null, Ahora, 5));
    }

    [Fact]
    public void CorrespondeAvisar_AntesDelTiempoMinimo_No()
    {
        Assert.False(AvisosWorker.CorrespondeAvisar(Ahora.AddMinutes(-2), Ahora, 5));
    }

    [Fact]
    public void CorrespondeAvisar_PasadoElTiempoMinimo_Si()
    {
        Assert.True(AvisosWorker.CorrespondeAvisar(Ahora.AddMinutes(-5), Ahora, 5));
    }

    [Theory]
    [InlineData("no-es-un-email", null, 1)]
    [InlineData(null, "1112345678", 1)]        // Falta el código de país
    [InlineData(null, "+54 9 11 1234", 1)]     // Espacios y muy corto
    [InlineData(null, null, -1)]
    [InlineData(null, null, 2000)]
    public void Validar_DatosInvalidos_Falla(string? email, string? telefono, int minutos)
    {
        var request = new ConfiguracionAvisosRequest(true, email, telefono, null, minutos);

        Assert.Throws<ArgumentException>(() => ConfiguracionAvisosService.Validar(request));
    }

    [Fact]
    public void Validar_DatosCorrectos_NoFalla()
    {
        var request = new ConfiguracionAvisosRequest(true, "dueno@ejemplo.com", "+5491112345678", "123456", 5);

        ConfiguracionAvisosService.Validar(request);
    }

    [Theory]
    [InlineData("clave", "clave", true)]
    [InlineData("otra", "clave", false)]
    [InlineData(null, "clave", false)]
    [InlineData("", "", false)]   // Si no hay clave configurada, nadie entra
    public void ClaveAcceso_ComparaCorrectamente(string? recibida, string? esperada, bool resultado)
    {
        Assert.Equal(resultado, ClaveAcceso.EsValida(recibida, esperada));
    }

    [Fact]
    public async Task Despachador_UnCanalFalla_LosDemasSeEnvianIgual()
    {
        var falla = new EnviadorFalso("Email", falla: true);
        var anda = new EnviadorFalso("WhatsApp", falla: false);
        var despachador = new DespachadorAvisos(new IEnviadorAviso[] { falla, anda }, NullLogger<DespachadorAvisos>.Instance);

        var resultados = await despachador.Enviar(new ConfiguracionAvisos(), AvisoDePrueba, CancellationToken.None);

        Assert.Equal(2, resultados.Count);
        Assert.StartsWith("Email: error", resultados[0]);
        Assert.Equal("WhatsApp: enviado", resultados[1]);
        Assert.True(anda.SeEnvio);
    }

    [Fact]
    public async Task Despachador_CanalSinConfigurar_NoSeUsa()
    {
        var sinConfigurar = new EnviadorFalso("Email", falla: false, configurado: false);
        var despachador = new DespachadorAvisos(new IEnviadorAviso[] { sinConfigurar }, NullLogger<DespachadorAvisos>.Instance);

        var resultados = await despachador.Enviar(new ConfiguracionAvisos(), AvisoDePrueba, CancellationToken.None);

        Assert.Empty(resultados);
        Assert.False(sinConfigurar.SeEnvio);
    }

    private class EnviadorFalso : IEnviadorAviso
    {
        private readonly bool _falla;
        private readonly bool _configurado;

        public EnviadorFalso(string nombre, bool falla, bool configurado = true)
        {
            Nombre = nombre;
            _falla = falla;
            _configurado = configurado;
        }

        public string Nombre { get; }
        public bool SeEnvio { get; private set; }

        public bool EstaConfigurado(ConfiguracionAvisos configuracion) => _configurado;

        public Task Enviar(ConfiguracionAvisos configuracion, Aviso aviso, CancellationToken ct)
        {
            if (_falla)
                throw new InvalidOperationException("sin conexión");
            SeEnvio = true;
            return Task.CompletedTask;
        }
    }
}
