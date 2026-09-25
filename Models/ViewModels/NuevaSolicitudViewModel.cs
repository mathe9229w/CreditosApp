using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models.ViewModels;

public class NuevaSolicitudViewModel
{
    [Required(ErrorMessage = "Ingrese el monto solicitado.")]
    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "El monto debe ser mayor a 0.")]
    [Display(Name = "Monto solicitado (S/)")]
    public decimal? MontoSolicitado { get; set; }

    // Solo informativo en la vista
    public decimal? IngresosMensuales { get; set; }
    public bool ClienteActivo { get; set; }
    public bool TieneCliente { get; set; }
}

public class PerfilClienteViewModel
{
    [Required(ErrorMessage = "Ingrese sus ingresos mensuales.")]
    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "Los ingresos mensuales deben ser mayores a 0.")]
    [Display(Name = "Ingresos mensuales (S/)")]
    public decimal? IngresosMensuales { get; set; }
}
