# Sistema de Ventas — Proceso de Extracción (E de ETL)

Worker Service en **.NET 8** que implementa la fase de **Extracción** del proceso ETL
para el Data Warehouse `DW_SistemaVentas`, alimentando en staging las futuras dimensiones
`DimProducto`, `DimCliente` y la tabla de hechos `FactVentas`.

## 0. Fuentes de datos reales del proyecto

| Archivo de origen | Fuente en la arquitectura | Extractor |
|---|---|---|
| `products.csv` | CSV | `CsvExtractor` → lee `Data/products.csv` directamente |
| `customers.csv` | Base de datos relacional | `DatabaseExtractor` → consulta la tabla `Customers` (ver `Scripts/Create_Customers_Table.sql` para crearla y cargarla desde el CSV) |
| `orders.csv` + `order_details.csv` | API REST | `ApiExtractor` → consume `GET api/ventas`, que debe devolver las órdenes ya combinadas con su detalle (un JSON por línea de producto). `Data/ventas_sample.json` trae una muestra de 50 registros ya combinados, útil para levantar un mock local (ej. `json-server`) mientras no tengas la API real. |

**Nota:** `customers.csv` y `orders.csv`/`order_details.csv` no se leen directamente como CSV en el Worker — representan el contenido que, en un escenario real, ya vive en la base de datos relacional y en la API respectivamente. El script SQL y el JSON de ejemplo sirven para simular esas fuentes localmente.

## 1. Arquitectura

Se aplicó **Clean Architecture** en 4 capas, con dependencias apuntando siempre hacia el centro:

```
SistemaVentas.ETL.Worker            → Host, Program.cs, appsettings.json (capa externa)
        │
SistemaVentas.ETL.Infrastructure    → Extractores concretos (Csv, DB, API) + Staging
        │
SistemaVentas.ETL.Application       → Orquestador del proceso de extracción
        │
SistemaVentas.ETL.Domain            → Entidades + interfaces (IExtractor, IStagingWriter)
```

```
                ┌───────────────────────────┐
                │   SistemaVentas.ETL.Worker │
                │   (BackgroundService)      │
                └─────────────┬───────────────┘
                              │
                ┌─────────────▼───────────────┐
                │  ExtractionOrchestrator      │
                │  (Application)                │
                └───┬───────────┬───────────┬──┘
                    │           │           │
         ┌──────────▼─┐  ┌──────▼──────┐ ┌──▼───────────┐
         │ CsvExtractor│  │DatabaseExtr.│ │ ApiExtractor │
         │  (Productos)│  │ (Clientes)  │ │  (Ventas)    │
         └──────┬──────┘  └──────┬──────┘ └──────┬───────┘
                │                │                │
        ┌───────▼────────────────▼────────────────▼───────┐
        │            JsonStagingWriter<T>                  │
        │        (Staging/Producto_*.json, etc.)           │
        └───────────────────────┬───────────────────────────┘
                                 │
                    (siguiente fase: Transformación y Carga
                     hacia DW_SistemaVentas)
```

**Componentes:**

| Componente | Responsabilidad |
|---|---|
| `IExtractor<T>` | Contrato único para cualquier fuente de extracción |
| `CsvExtractor` | Lee `Data/productos.csv` con CsvHelper |
| `DatabaseExtractor` | Consulta clientes en una BD relacional con Dapper/ADO.NET |
| `ApiExtractor` | Consume ventas desde una API REST vía `IHttpClientFactory` |
| `IStagingWriter<T>` | Contrato de persistencia intermedia |
| `JsonStagingWriter<T>` | Guarda cada extracción como JSON en `Staging/` |
| `ExtractionOrchestrator` | Ejecuta las 3 extracciones en paralelo y registra logs |
| `EtlWorker` | `BackgroundService` que dispara el ciclo ETL periódicamente |

## 2. Cumplimiento de atributos de calidad

**Rendimiento:** las tres extracciones (`CsvExtractor`, `DatabaseExtractor`, `ApiExtractor`)
se ejecutan en paralelo con `Task.WhenAll` dentro de `ExtractionOrchestrator`, y cada una
mide su tiempo con `Stopwatch`, registrado vía `ILogger`.

**Escalabilidad:** agregar una nueva fuente solo requiere crear una clase que implemente
`IExtractor<T>` y registrarla en `Program.cs`; el orquestador y el resto del pipeline no
necesitan cambios (principio Open/Closed).

**Seguridad:** las cadenas de conexión y API keys se centralizan en `appsettings.json`
y nunca están hardcodeadas en el código. En un entorno real, `appsettings.Development.json`,
variables de entorno o **Secret Manager** (`dotnet user-secrets`) deben usarse para no subir
credenciales al repositorio (ver `.gitignore`).

**Mantenibilidad:** separación estricta en 4 capas (Domain/Application/Infrastructure/Worker),
uso de interfaces para desacoplar (`IExtractor<T>`, `IStagingWriter<T>`) e inyección de
dependencias configurada en `Program.cs`.

## 3. Cómo ejecutar

```bash
cd SistemaVentas.ETL
dotnet restore
dotnet build

# Configurar secretos de forma segura antes de correr (recomendado):
cd SistemaVentas.ETL.Worker
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OrigenClientes" "Server=...;Database=...;..."
dotnet user-secrets set "VentasApi:ApiKey" "tu-api-key"

dotnet run
```

Al ejecutarse, el Worker:
1. Extrae productos desde `Data/productos.csv`.
2. Extrae clientes desde la base de datos configurada en `ConnectionStrings:OrigenClientes`.
3. Extrae ventas desde la API configurada en `VentasApi:BaseUrl`.
4. Guarda cada resultado como JSON en la carpeta `Staging/`.
5. Repite el ciclo cada `EtlSettings:IntervaloMinutos` (60 min por defecto).

## 4. Próximos pasos (fuera del alcance de esta entrega)

- Implementar la **Transformación** (limpieza, mapeo de claves de negocio a claves
  sustitutas) y la **Carga** hacia `DW_SistemaVentas` (ver `DataLoader` sugerido en la guía).
- Sustituir `JsonStagingWriter` por una implementación que inserte en tablas
  `Staging_Producto`, `Staging_Cliente`, `Staging_Venta` en SQL Server.

## 5. Modelo de datos destino

El detalle completo del modelo dimensional (esquema en estrella, diccionario de tablas
y script SQL) se encuentra en el documento *"Modelado de la Base de Datos - Sistema de
Ventas"* entregado junto con este proyecto.
