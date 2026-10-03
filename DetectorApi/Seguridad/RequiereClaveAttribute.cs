using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DetectorApi.Seguridad;

// Filtro que corre antes del endpoint: si el header no trae la clave correcta, responde 401 y el endpoint no se ejecuta.
public abstract class RequiereClaveAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _header;
    private readonly string _claveEnConfiguracion;

    protected RequiereClaveAttribute(string header, string claveEnConfiguracion)
    {
        _header = header;
        _claveEnConfiguracion = claveEnConfiguracion;
    }

    public void OnAuthorization(AuthorizationFilterContext contexto)
    {
        var configuracion = contexto.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        string? recibida = contexto.HttpContext.Request.Headers[_header];
        if (!ClaveAcceso.EsValida(recibida, configuracion[_claveEnConfiguracion]))
            contexto.Result = new UnauthorizedObjectResult($"Falta el header {_header} o la clave no es válida.");
    }
}

// Para el planificador y las cámaras que mandan imágenes.
public class RequiereApiKeyAttribute : RequiereClaveAttribute
{
    public const string HEADER = "X-Api-Key";
    public RequiereApiKeyAttribute() : base(HEADER, "Seguridad:ApiKey") { }
}

// Para la configuración, que solo puede tocar el dueño.
public class RequiereClaveAdminAttribute : RequiereClaveAttribute
{
    public const string HEADER = "X-Clave-Admin";
    public RequiereClaveAdminAttribute() : base(HEADER, "Seguridad:ClaveAdministrador") { }
}
