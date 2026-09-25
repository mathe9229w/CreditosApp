using System.Security.Claims;
using CreditosApp.Models.ViewModels;
using CreditosApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditosApp.Controllers;

[Authorize]
public class SolicitudesController(ISolicitudService solicitudes) : Controller
{
    private string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /Solicitudes  -> "Mis solicitudes"
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FiltroSolicitudes filtro)
    {
        var todas = await solicitudes.ListarDelUsuarioAsync(UsuarioId);
        var cliente = await solicitudes.ObtenerClienteAsync(UsuarioId);

        // Validación server-side: si los filtros son inválidos se informa y no se aplican.
        var lista = ModelState.IsValid ? filtro.Aplicar(todas).ToList() : todas.ToList();

        return View(new MisSolicitudesViewModel
        {
            Filtro = filtro,
            Solicitudes = lista,
            TieneCliente = cliente is not null
        });
    }

    // GET /Solicitudes/Detalle/5
    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var solicitud = await solicitudes.ObtenerDelUsuarioAsync(id, UsuarioId);
        if (solicitud is null) return NotFound();
        return View(solicitud);
    }

    // GET /Solicitudes/Crear
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        return View(await ConstruirFormularioAsync(new NuevaSolicitudViewModel()));
    }

    // POST /Solicitudes/Crear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(NuevaSolicitudViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Revise los datos del formulario.";
            return View(await ConstruirFormularioAsync(model));
        }

        var resultado = await solicitudes.CrearAsync(UsuarioId, model.MontoSolicitado!.Value);
        if (!resultado.Exito)
        {
            ViewBag.Error = resultado.Mensaje;
            return View(await ConstruirFormularioAsync(model));
        }

        // Feedback en la misma vista
        ModelState.Clear();
        ViewBag.Exito = resultado.Mensaje;
        ViewBag.SolicitudId = resultado.SolicitudId;
        return View(await ConstruirFormularioAsync(new NuevaSolicitudViewModel()));
    }

    // GET/POST /Solicitudes/Perfil  (ingresos mensuales del cliente)
    [HttpGet]
    public async Task<IActionResult> Perfil()
    {
        var cliente = await solicitudes.ObtenerClienteAsync(UsuarioId);
        return View(new PerfilClienteViewModel { IngresosMensuales = cliente?.IngresosMensuales });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Perfil(PerfilClienteViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var resultado = await solicitudes.GuardarPerfilAsync(UsuarioId, model.IngresosMensuales!.Value);
        if (resultado.Exito) ViewBag.Exito = resultado.Mensaje; else ViewBag.Error = resultado.Mensaje;
        return View(model);
    }

    private async Task<NuevaSolicitudViewModel> ConstruirFormularioAsync(NuevaSolicitudViewModel model)
    {
        var cliente = await solicitudes.ObtenerClienteAsync(UsuarioId);
        model.TieneCliente = cliente is not null;
        model.ClienteActivo = cliente?.Activo ?? false;
        model.IngresosMensuales = cliente?.IngresosMensuales;
        return model;
    }
}
