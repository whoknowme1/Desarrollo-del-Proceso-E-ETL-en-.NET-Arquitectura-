# DataOpinionsExtractor

Worker Service en .NET 8 que extrae opiniones de clientes desde archivos CSV, SQL Server y una API REST, y las guarda como archivos JSON (staging).

## Requisitos

- .NET 8 SDK
- SQL Server con la base `DataOpinions`
- Carpeta con los archivos CSV de origen
- Acceso a internet (API de prueba `jsonplaceholder.typicode.com`)

## Estructura del proyecto

```
WorkerService/
├── Domain/                  ExtractionResult
├── Application/
│   ├── Interfaces/          IExtractor
│   ├── Options/             ExtractionOptions (configuración tipada)
│   └── Services/            ExtractionService (orquesta la extracción)
├── Infrastructure/
│   └── Extractor/           CsvExtractor, DatabaseExtractor, ApiExtractor
├── Worker.cs                Arranca la extracción y cierra el proceso
├── Program.cs               Registro de servicios y configuración
└── appsettings.json         Configuración base (sin datos sensibles)
```


## Configuración

Crear `WorkerService/appsettings.Development.json` (no se estara en el repositorio):

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

## Ejecución

```
dotnet run --project WorkerService
```

Requiere .NET 8 SDK y SQL Server con la base `DataOpinions`.

El proceso se cierra solo al terminar. Código de salida 0 si todas las fuentes salieron bien y 1 si alguna falló (las demás se completan igual).

## Salida (staging)

```
<OutputFolderPath>/
├── CSV/        un .json por cada .csv
├── Database/   un .json por tabla (esquema.tabla.json)
├── API/        ApiComments.json
└── extraction_summary.json   registros, tiempos y estado de cada fuente
```

## Logs

Los eventos y errores se registran con `ILogger` en la consola, con el prefijo de cada fuente (`[CSV]`, `[Database]`, `[API]`). Al final se muestra un resumen por fuente con registros y tiempo, y el tiempo total de la extracción. Los fallos de una fuente se registran con su excepción y no detienen a las demás.

## Diseño

- `Worker` arranca la extracción y cierra el proceso.
- `ExtractionService` ejecuta los extractores en paralelo y registra tiempos y errores.
- `IExtractor` abstrae cada fuente (`CsvExtractor`, `DatabaseExtractor`, `ApiExtractor`). Para agregar una fuente, se crea una clase que lo implemente y se registra en `Program.cs`.
- La configuración está en `appsettings.json`; las credenciales van fuera del repositorio.
