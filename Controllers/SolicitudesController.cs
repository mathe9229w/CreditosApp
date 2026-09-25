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
}
