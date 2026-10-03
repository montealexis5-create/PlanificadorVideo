using DetectorApi.Entidades;

namespace DetectorApi.Servicios.Avisos;

// Un canal por el que se avisa al dueño (email, WhatsApp...). Para sumar otro canal alcanza con implementar esta interfaz.
public interface IEnviadorAviso
{
    string Nombre { get; }
    bool EstaConfigurado(ConfiguracionAvisos configuracion);
    Task Enviar(ConfiguracionAvisos configuracion, Aviso aviso, CancellationToken ct);
}
