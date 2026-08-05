namespace SistemaVentas.ETL.Domain.Interfaces;

/// <summary>
/// Contrato genérico para cualquier proceso de extracción (E de ETL),
/// sin importar la fuente de origen (CSV, base de datos, API REST, etc.).
/// Aplica el principio de inversión de dependencias (SOLID - D).
/// </summary>
/// <typeparam name="T">Tipo de entidad extraída.</typeparam>
public interface IExtractor<T>
{
    /// <summary>
    /// Nombre descriptivo de la fuente, usado para logging/trazabilidad.
    /// </summary>
    string NombreFuente { get; }

    /// <summary>
    /// Ejecuta la extracción de datos desde la fuente correspondiente.
    /// </summary>
    Task<IReadOnlyList<T>> ExtractAsync(CancellationToken cancellationToken = default);
}
