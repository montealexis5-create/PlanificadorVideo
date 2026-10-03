using DetectorApi.Dtos;
using DetectorApi.Servicios;

namespace PlanificadorVideo.Tests;

public class SupresionDeCajasTests
{
    private const double UMBRAL = 0.45;

    [Fact]
    public void Superposicion_CajasIguales_EsUno()
    {
        var caja = new CajaDto(0.1, 0.1, 0.2, 0.4, 0.9);

        Assert.Equal(1, SupresionDeCajas.Superposicion(caja, caja), 5);
    }

    [Fact]
    public void Superposicion_CajasSeparadas_EsCero()
    {
        var izquierda = new CajaDto(0.0, 0.0, 0.2, 0.2, 0.9);
        var derecha = new CajaDto(0.5, 0.5, 0.2, 0.2, 0.9);

        Assert.Equal(0, SupresionDeCajas.Superposicion(izquierda, derecha));
    }

    [Fact]
    public void Superposicion_MitadSuperpuesta_EsUnTercio()
    {
        // Comparten la mitad de su área: intersección 0.02 / unión 0.06.
        var a = new CajaDto(0.0, 0.0, 0.2, 0.2, 0.9);
        var b = new CajaDto(0.1, 0.0, 0.2, 0.2, 0.9);

        Assert.Equal(1.0 / 3, SupresionDeCajas.Superposicion(a, b), 5);
    }

    [Fact]
    public void QuitarSuperpuestas_MismaPersonaDosVeces_DejaLaDeMayorConfianza()
    {
        var debil = new CajaDto(0.10, 0.10, 0.20, 0.50, 0.60);
        var fuerte = new CajaDto(0.11, 0.10, 0.20, 0.50, 0.90);

        var resultado = SupresionDeCajas.QuitarSuperpuestas(new[] { debil, fuerte }, UMBRAL);

        Assert.Equal(new[] { fuerte }, resultado);
    }

    [Fact]
    public void QuitarSuperpuestas_DosPersonasSeparadas_DejaAmbas()
    {
        var personaA = new CajaDto(0.05, 0.10, 0.20, 0.60, 0.80);
        var personaB = new CajaDto(0.60, 0.10, 0.20, 0.60, 0.70);

        var resultado = SupresionDeCajas.QuitarSuperpuestas(new[] { personaA, personaB }, UMBRAL);

        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public void QuitarSuperpuestas_SinCandidatas_DevuelveListaVacia()
    {
        Assert.Empty(SupresionDeCajas.QuitarSuperpuestas(new List<CajaDto>(), UMBRAL));
    }
}
