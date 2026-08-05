namespace SistemaVentas.ETL.Domain.Interfaces;

/// <summary>
/// Contrato para persistir los datos extraídos en un área de staging
/// (archivos temporales o tablas staging), previo a la fase de transformación/carga.
/// </summary>
/// <typeparam name="T">Tipo de entidad a almacenar.</typeparam>
public interface IStagingWriter<T>
{
    Task WriteAsync(IReadOnlyList<T> registros, string nombreLote, CancellationToken cancellationToken = default);
}
