-- Script para crear la tabla de origen "Customers" que consulta DatabaseExtractor.
-- Esta tabla simula la base de datos relacional externa de la que se extraen
-- los clientes (equivalente a los datos de customers.csv).

CREATE DATABASE OrigenVentas;
GO
USE OrigenVentas;
GO

CREATE TABLE Customers (
    CustomerID INT NOT NULL PRIMARY KEY,
    FirstName  VARCHAR(100) NOT NULL,
    LastName   VARCHAR(100) NOT NULL,
    Email      VARCHAR(150) NULL,
    Phone      VARCHAR(50)  NULL,
    City       VARCHAR(100) NULL,
    Country    VARCHAR(100) NULL
);
GO

-- Carga masiva desde customers.csv (ajusta la ruta local antes de ejecutar).
-- El archivo tiene encabezado, por eso FIRSTROW = 2.
BULK INSERT Customers
FROM 'C:\ruta\a\customers.csv'
WITH (
    FIRSTROW = 2,
    FIELDTERMINATOR = ',',
    ROWTERMINATOR = '\n',
    CODEPAGE = '65001',   -- UTF-8
    TABLOCK
);
GO

-- Verificación rápida
SELECT COUNT(*) AS TotalClientes FROM Customers;
SELECT TOP 5 * FROM Customers;
