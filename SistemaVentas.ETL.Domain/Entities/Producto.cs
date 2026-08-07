namespace SistemaVentas.ETL.Domain.Entities;


/// Representa un producto extraído desde el archivo CSV.
/// Corresponde al staging previo de DimProducto.
public class Producto
{
    public string ProductoID { get; set; } = string.Empty;
    public string NombreProducto { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string? Subcategoria { get; set; }
    public decimal PrecioUnitario { get; set; }
    public string Estado { get; set; } = "Activo";
}
