using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataOpinionsExtractor.Domain
{
   
 public record ExtractionResult(
    string Source,
    int Records,
    TimeSpan Elapsed,
    bool Success,
    string? Error = null);

    }
