using Microsoft.AspNetCore.Diagnostics;

namespace DetectorApi.Seguridad;

// Manejo centralizado de errores: los servicios lanzan ArgumentException con un mensaje claro y acá se
// convierte en un 400. Así los controladores no repiten try/catch. Cualquier otro error queda como 500
// sin mostrar detalles internos (se compara el tipo exacto para no exponer mensajes de librerías).
public class ManejadorDeErrores : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        if (excepcion.GetType() != typeof(ArgumentException))
            return false;

        contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
        contexto.Response.ContentType = "text/plain; charset=utf-8";
        await contexto.Response.WriteAsync(excepcion.Message, ct);
        return true;
    }
}
