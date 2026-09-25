using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models.ViewModels;

/// <summary>Filtros de "Mis solicitudes". Se validan en el servidor (IValidatableObject).</summary>
public class FiltroSolicitudes : IValidatableObject
{
    public EstadoSolicitud? Estado { get; set; }

    [Display(Name = "Monto mínimo")]
    public decimal? MontoMin { get; set; }

    [Display(Name = "Monto máximo")]
    public decimal? MontoMax { get; set; }

    [Display(Name = "Fecha inicio")]
    [DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fecha fin")]
    [DataType(DataType.Date)]
    public DateTime? FechaFin { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MontoMin < 0)
            yield return new ValidationResult("El monto mínimo no puede ser negativo.", [nameof(MontoMin)]);
        if (MontoMax < 0)
            yield return new ValidationResult("El monto máximo no puede ser negativo.", [nameof(MontoMax)]);
        if (MontoMin.HasValue && MontoMax.HasValue && MontoMin > MontoMax)
            yield return new ValidationResult("El monto mínimo no puede ser mayor que el monto máximo.", [nameof(MontoMin), nameof(MontoMax)]);
        if (FechaInicio.HasValue && FechaFin.HasValue && FechaInicio.Value.Date > FechaFin.Value.Date)
            yield return new ValidationResult("La fecha de inicio no puede ser mayor que la fecha fin.", [nameof(FechaInicio), nameof(FechaFin)]);
    }

    public IEnumerable<SolicitudResumen> Aplicar(IEnumerable<SolicitudResumen> origen)
    {
        var q = origen;
        if (Estado.HasValue) q = q.Where(s => s.Estado == Estado.Value);
        if (MontoMin.HasValue) q = q.Where(s => s.MontoSolicitado >= MontoMin.Value);
        if (MontoMax.HasValue) q = q.Where(s => s.MontoSolicitado <= MontoMax.Value);
        // Las fechas se guardan en UTC; el filtro compara por día calendario.
        if (FechaInicio.HasValue) q = q.Where(s => s.FechaSolicitud.Date >= FechaInicio.Value.Date);
        if (FechaFin.HasValue) q = q.Where(s => s.FechaSolicitud.Date <= FechaFin.Value.Date);
        return q.OrderByDescending(s => s.FechaSolicitud);
    }
}
