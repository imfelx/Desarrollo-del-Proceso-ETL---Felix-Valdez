namespace SistemaVentas.ETL.Domain.Interfaces;

/// Contrato para persistir los datos extraídos en un área de staging
/// (archivos temporales o tablas staging), previo a la fase de transformación/carga.

public interface IStagingWriter<T>
{
    Task WriteAsync(IReadOnlyList<T> registros, string nombreLote, CancellationToken cancellationToken = default);
}
