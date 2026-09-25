using CreditosApp.Data;
using CreditosApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditosApp.Controllers;

[Authorize(Roles = DbSeeder.RolAnalista)]
[Route("Analista")]
public class AnalistaController(IEvaluacionService evaluacion) : Controller
{
    // GET /Analista
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.NoEncoladas = await evaluacion.ListarNoEncoladasAsync();
        return View(await evaluacion.ListarPendientesAsync());
    }

    // POST /Analista/Aprobar/5
    [HttpPost("Aprobar/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var r = await evaluacion.AprobarAsync(id);
        TempData[r.Exito ? "Exito" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    // POST /Analista/Rechazar/5
    [HttpPost("Rechazar/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string? motivoRechazo)
    {
        var r = await evaluacion.RechazarAsync(id, motivoRechazo);
        TempData[r.Exito ? "Exito" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    // POST /Analista/ReenviarNotificacion  (Cloud MQ: reenvío manual con el mismo MessageId)
    [HttpPost("ReenviarNotificacion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReenviarNotificacion(int solicitudId)
    {
        var r = await evaluacion.ReenviarNotificacionAsync(solicitudId);
        TempData[r.Exito ? "Exito" : "Error"] = r.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
