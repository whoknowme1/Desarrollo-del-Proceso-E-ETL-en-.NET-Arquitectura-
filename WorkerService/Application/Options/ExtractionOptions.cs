using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataOpinionsExtractor.Application.Options
{
    public class ExtractionOptions
    {
        public const string SectionName = "Extraction";

        public string CsvFolderPath { get; set; } = string.Empty;

        public string OutputFolderPath { get; set; } = string.Empty;

        public int CsvMaxParallelism { get; set; } = 4;

        public List<string> DatabaseTables { get; set; } = new();

        public int DatabaseMaxParallelism { get; set; } = 3;
        public int DatabaseCommandTimeoutSeconds { get; set; } = 120;

        public string ApiUrl { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;
        public string ApiKeyHeader { get; set; } = "X-Api-Key";
        public int ApiTimeoutSeconds { get; set; } = 30;
        public int ApiMaxRetries { get; set; } = 3;
    }
}