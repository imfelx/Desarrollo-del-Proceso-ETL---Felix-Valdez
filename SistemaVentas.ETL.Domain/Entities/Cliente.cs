namespace SistemaVentas.ETL.Domain.Entities;

/// Representa un cliente extraído desde la base de datos relacional de origen.
/// Corresponde al staging previo de DimCliente.

public class Cliente
{
    public string ClienteID { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public string? Segmento { get; set; }
    public string Pais { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? Ciudad { get; set; }
}
