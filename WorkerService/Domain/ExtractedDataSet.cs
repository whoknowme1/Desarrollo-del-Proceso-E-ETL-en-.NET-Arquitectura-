using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataOpinionsExtractor.Domain;

public sealed record ExtractedDataset(
    string Name,
    IAsyncEnumerable<IReadOnlyDictionary<string, object?>> Rows);