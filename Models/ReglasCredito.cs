namespace CreditosApp.Models;

/// <summary>Reglas de negocio centralizadas (se validan siempre en el servidor).</summary>
public static class ReglasCredito
{
    /// <summary>Un analista no puede aprobar si MontoSolicitado &gt; 5 x IngresosMensuales.</summary>
    public const decimal FactorMaximoAprobacion = 5m;

    /// <summary>Al registrar, MontoSolicitado no puede superar 10 x IngresosMensuales.</summary>
    public const decimal FactorMaximoRegistro = 10m;

    public static bool PuedeAprobarse(decimal monto, decimal ingresos) =>
        monto <= ingresos * FactorMaximoAprobacion;

    public static bool PuedeRegistrarse(decimal monto, decimal ingresos) =>
        monto <= ingresos * FactorMaximoRegistro;
}
