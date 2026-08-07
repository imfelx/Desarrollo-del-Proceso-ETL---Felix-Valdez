namespace SistemaVentas.ETL.Domain.Interfaces;

/// Contrato para garantizar que DimVendedor tenga al menos el registro
/// "miembro desconocido" (patrón Kimball), usado mientras el proceso de
/// extracción no cuente con una fuente real de vendedores.

public interface IVendedorSeeder
{
    string TablaDestino { get; }

    Task<int> EnsureDefaultAsync(CancellationToken cancellationToken = default);
}
