# Sistema de Ventas — Proceso ETL (Extracción y Carga)

Worker Service en **.NET 8** que implementa las fases de **Extracción** y **Carga** del
proceso ETL para el Data Warehouse `DW_SistemaVentas`: extrae desde 3 fuentes distintas,
guarda en staging (JSON) y carga las dimensiones `DimProducto`, `DimCliente`, `DimTiempo`
y `DimVendedor` en el esquema en estrella.

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
                ┌────────────────▼────────────────┐
                │      LoadOrchestrator            │
                │      (Application)               │
                └───┬─────────┬─────────┬────────┬─┘
                    │         │         │        │
         ┌──────────▼─┐ ┌─────▼─────┐ ┌─▼───────┐┌▼──────────────┐
         │SqlProducto  │ │SqlCliente │ │SqlTiempo││SqlVendedor    │
         │Loader       │ │Loader     │ │Loader   ││Seeder         │
         └──────┬──────┘ └─────┬─────┘ └────┬────┘└───────┬───────┘
                │              │            │             │
                ▼              ▼            ▼             ▼
           DimProducto    DimCliente   DimTiempo    DimVendedor
                     (DW_SistemaVentas — SQL Server)
```

**Componentes — Extracción:**

| Componente | Responsabilidad |
|---|---|
| `IExtractor<T>` | Contrato único para cualquier fuente de extracción |
| `CsvExtractor` | Lee `Data/productos.csv` con CsvHelper |
| `DatabaseExtractor` | Consulta clientes en una BD relacional con Dapper/ADO.NET |
| `ApiExtractor` | Consume ventas desde una API REST vía `IHttpClientFactory` |
| `IStagingWriter<T>` | Contrato de persistencia intermedia |
| `JsonStagingWriter<T>` | Guarda cada extracción como JSON en `Staging/` |
| `ExtractionOrchestrator` | Ejecuta las 3 extracciones en paralelo y registra logs |

**Componentes — Carga:**

| Componente | Responsabilidad |
|---|---|
| `IStagingReader<T>` / `JsonStagingReader<T>` | Lee de vuelta el último lote de `Staging/` |
| `IDimensionLoader<T>` | Contrato de carga (upsert) hacia una dimensión |
| `SqlProductoLoader` | MERGE de productos hacia `DimProducto` por `ProductoID` |
| `SqlClienteLoader` | MERGE de clientes hacia `DimCliente` por `ClienteID` |
| `SqlTiempoLoader` (`ITiempoLoader`) | Genera y carga `DimTiempo` a partir del rango de fechas de las ventas |
| `SqlVendedorSeeder` (`IVendedorSeeder`) | Garantiza el miembro por defecto `VEND-000 - Sin asignar` en `DimVendedor` |
| `LoadOrchestrator` | Ejecuta la carga de las 4 dimensiones en secuencia y registra logs |
| `EtlWorker` | `BackgroundService` que dispara Extracción → Carga en cada ciclo |

**Sobre `DimVendedor`:** la fuente de ventas actual (API REST) no expone un identificador
de vendedor, por lo que no hay datos reales que cargar todavía. Se aplicó el patrón de
"miembro desconocido" (Kimball): se garantiza una única fila `VEND-000 = Sin asignar` para
que `FactVentas` siempre tenga una `VendedorKey` válida. Cuando exista una fuente real de
vendedores, bastará con crear un `IExtractor<Vendedor>` y un `IDimensionLoader<Vendedor>`
siguiendo el mismo patrón que `Producto`/`Cliente` (Open/Closed).

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

Antes de correr, crea el DW ejecutando `Scripts/Create_DW_SistemaVentas.sql` contra tu
instancia de SQL Server y configura `ConnectionStrings:DW_SistemaVentas` (por
`dotnet user-secrets` o en `appsettings.json`).

Al ejecutarse, el Worker repite este ciclo cada `EtlSettings:IntervaloMinutos` (60 min
por defecto):

**Fase E (Extracción):**
1. Extrae productos desde `Data/products.csv`.
2. Extrae clientes desde la base de datos configurada en `ConnectionStrings:OrigenAnalisisVentas`.
3. Extrae ventas desde la API configurada (`AnalisisVentasApi`).
4. Guarda cada resultado como JSON en la carpeta `Staging/`.

**Fase L (Carga):**
5. Lee el último lote de `Staging/` para cada entidad.
6. Aplica MERGE (upsert) de productos hacia `DimProducto` y de clientes hacia `DimCliente`.
7. Genera y carga `DimTiempo` a partir del rango de fechas de las ventas extraídas.
8. Verifica el miembro por defecto de `DimVendedor`.

## 4. Próximos pasos (fuera del alcance de esta entrega)

- Incorporar una fuente real de vendedores (`IExtractor<Vendedor>` + `IDimensionLoader<Vendedor>`)
  para reemplazar el miembro por defecto de `DimVendedor`.
- Implementar la carga de `FactVentas`, resolviendo las claves sustitutas (`ProductoKey`,
  `ClienteKey`, `VendedorKey`, `FechaKey`) a partir de las llaves de negocio del staging de ventas.
- Migrar la ejecución de MERGE fila-por-fila a una carga masiva (`SqlBulkCopy` + tabla
  temporal) para lotes de mayor volumen.

## 5. Modelo de datos destino

El detalle completo del modelo dimensional (esquema en estrella, diccionario de tablas
y script SQL) se encuentra en `Scripts/Create_DW_SistemaVentas.sql`, provisto en el
documento *"Modelado de la Base de Datos - Sistema de Ventas"* entregado junto con este
proyecto.
