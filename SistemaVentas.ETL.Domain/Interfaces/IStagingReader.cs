namespace SistemaVentas.ETL.Domain.Interfaces;


/// Contrato para leer de vuelta el lote de staging más reciente

public interface IStagingReader<T>
{
    Task<IReadOnlyList<T>> ReadLatestAsync(CancellationToken cancellationToken = default);
}
