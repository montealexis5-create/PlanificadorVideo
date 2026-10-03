using Planificador;

namespace PlanificadorVideo.Tests;

public class FiltroMovimientoTests
{
    [Fact]
    public void Clasificar_CambianMuchosBloques_EsMovimientoDeCamara()
    {
        var resultado = FiltroMovimiento.Clasificar(ratio: 0.10, ratioBloquesConCambio: 0.80);

        Assert.False(resultado.DebeEnviar);
        Assert.True(resultado.CambioGlobalDetectado);
    }

    [Theory]
    [InlineData(0.015)]   // Justo en el mínimo
    [InlineData(0.10)]
    [InlineData(0.45)]    // Justo en el máximo
    public void Clasificar_CambioMedioEnPocosBloques_SeEnvia(double ratio)
    {
        var resultado = FiltroMovimiento.Clasificar(ratio, ratioBloquesConCambio: 0.20);

        Assert.True(resultado.DebeEnviar);
        Assert.False(resultado.CambioGlobalDetectado);
    }

    [Theory]
    [InlineData(0.0)]     // Imagen quieta
    [InlineData(0.01)]    // Ruido de la cámara
    [InlineData(0.50)]    // Demasiado cambio para ser una persona
    public void Clasificar_CambioFueraDelRango_NoSeEnvia(double ratio)
    {
        var resultado = FiltroMovimiento.Clasificar(ratio, ratioBloquesConCambio: 0.20);

        Assert.False(resultado.DebeEnviar);
        Assert.False(resultado.CambioGlobalDetectado);
    }
}
