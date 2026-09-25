using System.Security.Claims;
using CreditosApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Controllers;

[Authorize]
public class NotificacionesController(ApplicationDbContext db) : Controller
{
    // GET /Notificaciones -> "Mis notificaciones" (solo del usuario autenticado)
    public async Task<IActionResult> Index()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var lista = await db.Notificaciones
            .AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.Id)
            .ToListAsync();
        return View(lista);
    }
}
