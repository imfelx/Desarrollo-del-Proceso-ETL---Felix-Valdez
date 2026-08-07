namespace SistemaVentas.ETL.Domain.Interfaces;

/// Contrato genérico para cualquier proceso de extracción (E de ETL),
/// sin importar la fuente de origen (CSV, base de datos, API REST, etc.).
/// Aplica el principio de inversión de dependencias (SOLID - D).

public interface IExtractor<T>
{
    
    /// Nombre descriptivo de la fuente, usado para logging/trazabilidad.
 
    string NombreFuente { get; }

   
    /// Ejecuta la extracción de datos desde la fuente correspondiente.
   
    Task<IReadOnlyList<T>> ExtractAsync(CancellationToken cancellationToken = default);
}
