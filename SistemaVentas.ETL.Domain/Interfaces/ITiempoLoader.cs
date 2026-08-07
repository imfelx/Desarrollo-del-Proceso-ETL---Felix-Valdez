using SistemaVentas.ETL.Domain.Entities;

namespace SistemaVentas.ETL.Domain.Interfaces;

/// Contrato para cargar DimTiempo. A diferencia de las demás dimensiones,
/// no proviene de una fuente externa: se calcula a partir del rango de
/// fechas presente en las ventas extraídas (dimensión derivada).

public interface ITiempoLoader
{
    string TablaDestino { get; }

    Task<int> LoadAsync(IReadOnlyList<Venta> ventas, CancellationToken cancellationToken = default);
}
