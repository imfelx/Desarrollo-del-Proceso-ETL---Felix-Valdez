namespace SistemaVentas.ETL.Domain.Interfaces;

/// Contrato para cargar (L de ETL) un conjunto de registros ya extraídos
/// hacia una dimensión del Data Warehouse, aplicando upsert por llave de negocio.


public interface IDimensionLoader<T>
{
 
    /// Nombre de la tabla destino en el DW, usado para logging/trazabilidad.
  
    string TablaDestino { get; }

    
    /// Inserta o actualiza los registros en la tabla destino.
    /// Devuelve la cantidad de filas afectadas.
    
    Task<int> LoadAsync(IReadOnlyList<T> registros, CancellationToken cancellationToken = default);
}
