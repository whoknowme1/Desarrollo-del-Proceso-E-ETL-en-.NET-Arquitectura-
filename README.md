# DataOpinions: proceso de extracción (E de ETL)

Worker Service en .NET 8 que extrae opiniones de clientes desde archivos CSV, SQL Server y una API REST, y las guarda como archivos JSON (staging).

## Requisitos

- .NET 8 SDK
- SQL Server con la base `DataOpinions`
- Carpeta con los archivos CSV de origen

## Estructura del proyecto

```
WorkerService/
  Domain/            ExtractedDataset, ExtractionResult
  Application/       IExtractor, IStagingWriter, ExtractionService, ExtractionOptions
  Infrastructure/    CsvExtractor, DatabaseExtractor, ApiExtractor, JsonStagingWriter
  Program.cs         Registro de dependencias y configuración
DataOpinionsApi/     API mínima propia que sirve social_comments.csv

```

## Configuración y seguridad

La configuración está centralizada en `appsettings.json` (sin secretos ni rutas locales). Los valores locales y las credenciales se agregan en capas que no se suben al repositorio:

1. `appsettings.json` (en el repositorio, valores seguros por defecto)
2. `appsettings.Development.json` (ignorado por git)
3. User-secrets
4. Variables de entorno

```json
{
  "Extraction": {
    "CsvFolderPath": "C:\\ruta\\a\\los\\csv",
    "OutputFolderPath": "C:\\ruta\\de\\salida"
  },
  "ConnectionStrings": {
    "SourceDatabase": "Server=MI_SERVIDOR;Database=DataOpinions;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

## Cómo ejecutar

1. Configurar la llave de la API en `DataOpinionsApi/appsettings.Development.json`:
   ```json
   { "ApiSecurity": { "ApiKey": "<su-llave>" } }
   ```
2. Iniciar la API: `dotnet run --project DataOpinionsApi --launch-profile http` (escucha en `http://localhost:5080`).
3. Configurar las rutas y la cadena de conexión del Worker, y la misma llave de la API:
   ```
   dotnet user-secrets set "Extraction:ApiKey" "<su-llave>" --project WorkerService
   ```
4. Ejecutar el Worker: `dotnet run --project WorkerService`.

## Salida (staging)

```
<OutputFolderPath>/
├── CSV/        un .json por cada .csv
├── Database/   un .json por tabla (esquema.tabla.json)
├── API/        social-comments.json
└── extraction_summary.json   registros, tiempos y estado de cada fuente
```

## Logs

Los eventos y errores se registran con `ILogger` en la consola, con el prefijo de cada fuente (`[CSV]`, `[Database]`, `[API]`). Al final se muestra un resumen por fuente con registros y tiempo, y el tiempo total de la extracción. Los fallos de una fuente se registran con su excepción y no detienen a las demás.

## Diseño

- `Worker` arranca la extracción y cierra el proceso.
- `ExtractionService` ejecuta los extractores en paralelo y registra tiempos y errores.
- `IExtractor` abstrae cada fuente (`CsvExtractor`, `DatabaseExtractor`, `ApiExtractor`). Para agregar una fuente, se crea una clase que lo implemente y se registra en `Program.cs`.
- `IStagingWriter` (implementado por `JsonStagingWriter`) escribe cada conjunto de filas en JSON de forma atómica. Los extractores solo extraen, no saben dónde se guarda.
- La configuración está en `appsettings.json`; las credenciales van fuera del repositorio.
