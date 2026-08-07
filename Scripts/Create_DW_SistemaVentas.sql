/* ============================================================
   Script de creación - Data Warehouse Sistema de Ventas
   Modelo: Esquema en Estrella (Star Schema)
   Motor: SQL Server (compatible con adaptaciones menores en MySQL)
   ============================================================ */

CREATE DATABASE DW_SistemaVentas;
GO
USE DW_SistemaVentas;
GO

-- 1. DIMENSIÓN: DimTiempo

CREATE TABLE DimTiempo (
    FechaKey        INT             NOT NULL,
    Fecha           DATE            NOT NULL,
    Dia             TINYINT         NOT NULL,
    DiaSemana       VARCHAR(15)     NOT NULL,
    Mes             TINYINT         NOT NULL,
    NombreMes       VARCHAR(15)     NOT NULL,
    Trimestre       TINYINT         NOT NULL,
    Semestre        TINYINT         NOT NULL,
    Anio            SMALLINT        NOT NULL,
    CONSTRAINT PK_DimTiempo PRIMARY KEY (FechaKey)
);
GO

-- 2. DIMENSIÓN: DimProducto

CREATE TABLE DimProducto (
    ProductoKey     INT IDENTITY(1,1)   NOT NULL,
    ProductoID      VARCHAR(20)         NOT NULL,
    NombreProducto  VARCHAR(150)        NOT NULL,
    Categoria       VARCHAR(80)         NOT NULL,
    Subcategoria    VARCHAR(80)         NULL,
    PrecioUnitario  DECIMAL(12,2)       NOT NULL,
    Estado          VARCHAR(20)         NOT NULL DEFAULT 'Activo',
    FechaCarga      DATETIME            NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_DimProducto PRIMARY KEY (ProductoKey),
    CONSTRAINT UQ_DimProducto_BK UNIQUE (ProductoID)
);
GO

-- 3. DIMENSIÓN: DimCliente

CREATE TABLE DimCliente (
    ClienteKey      INT IDENTITY(1,1)   NOT NULL,
    ClienteID       VARCHAR(20)         NOT NULL,
    NombreCliente   VARCHAR(150)        NOT NULL,
    Segmento        VARCHAR(50)         NULL,
    Pais            VARCHAR(60)         NOT NULL,
    Region          VARCHAR(60)         NULL,
    Ciudad          VARCHAR(80)         NULL,
    FechaCarga      DATETIME            NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_DimCliente PRIMARY KEY (ClienteKey),
    CONSTRAINT UQ_DimCliente_BK UNIQUE (ClienteID)
);
GO

-- 4. DIMENSIÓN: DimVendedor

CREATE TABLE DimVendedor (
    VendedorKey     INT IDENTITY(1,1)   NOT NULL,
    VendedorID      VARCHAR(20)         NOT NULL,
    NombreVendedor  VARCHAR(150)        NOT NULL,
    Region          VARCHAR(60)         NULL,
    CONSTRAINT PK_DimVendedor PRIMARY KEY (VendedorKey),
    CONSTRAINT UQ_DimVendedor_BK UNIQUE (VendedorID)
);
GO

-- 5. TABLA DE HECHOS: FactVentas
-- Grano: una fila por línea de venta (detalle de factura)

CREATE TABLE FactVentas (
    VentaKey        BIGINT IDENTITY(1,1)   NOT NULL,
    FechaKey        INT                    NOT NULL,
    ProductoKey     INT                    NOT NULL,
    ClienteKey      INT                    NOT NULL,
    VendedorKey     INT                    NOT NULL,
    NumeroFactura   VARCHAR(20)            NOT NULL,
    Cantidad        INT                    NOT NULL,
    PrecioUnitario  DECIMAL(12,2)          NOT NULL,
    Descuento       DECIMAL(12,2)          NOT NULL DEFAULT 0,
    MontoTotal      DECIMAL(14,2)          NOT NULL,
    FechaCarga      DATETIME               NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_FactVentas PRIMARY KEY (VentaKey),
    CONSTRAINT FK_FactVentas_Tiempo    FOREIGN KEY (FechaKey)    REFERENCES DimTiempo(FechaKey),
    CONSTRAINT FK_FactVentas_Producto  FOREIGN KEY (ProductoKey) REFERENCES DimProducto(ProductoKey),
    CONSTRAINT FK_FactVentas_Cliente   FOREIGN KEY (ClienteKey)  REFERENCES DimCliente(ClienteKey),
    CONSTRAINT FK_FactVentas_Vendedor  FOREIGN KEY (VendedorKey) REFERENCES DimVendedor(VendedorKey),
    CONSTRAINT CK_FactVentas_Cantidad  CHECK (Cantidad > 0),
    CONSTRAINT CK_FactVentas_Monto     CHECK (MontoTotal >= 0)
);
GO

-- 6. Índices de apoyo para consultas analíticas

CREATE NONCLUSTERED INDEX IX_FactVentas_Fecha    ON FactVentas (FechaKey);
CREATE NONCLUSTERED INDEX IX_FactVentas_Producto ON FactVentas (ProductoKey);
CREATE NONCLUSTERED INDEX IX_FactVentas_Cliente  ON FactVentas (ClienteKey);
CREATE NONCLUSTERED INDEX IX_FactVentas_Vendedor ON FactVentas (VendedorKey);
GO

-- 7. Ejemplos de consultas que responden a los KPI solicitados

-- Total de ventas global
SELECT SUM(MontoTotal) AS TotalVentas FROM FactVentas;

-- Top 5 productos más vendidos (por monto)
SELECT TOP 5 p.NombreProducto, SUM(f.MontoTotal) AS IngresoTotal
FROM FactVentas f
JOIN DimProducto p ON f.ProductoKey = p.ProductoKey
GROUP BY p.NombreProducto
ORDER BY IngresoTotal DESC;

-- Ventas por mes y año
SELECT t.Anio, t.Mes, SUM(f.MontoTotal) AS VentasMes
FROM FactVentas f
JOIN DimTiempo t ON f.FechaKey = t.FechaKey
GROUP BY t.Anio, t.Mes
ORDER BY t.Anio, t.Mes;

-- Top 5 clientes por ingresos generados
SELECT TOP 5 c.NombreCliente, SUM(f.MontoTotal) AS TotalCompras
FROM FactVentas f
JOIN DimCliente c ON f.ClienteKey = c.ClienteKey
GROUP BY c.NombreCliente
ORDER BY TotalCompras DESC;
