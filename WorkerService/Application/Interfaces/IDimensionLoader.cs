using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataOpinionsExtractor.Application.Interfaces
{
    public interface IDimensionLoader
    {
        Task<int> LoadAsync(
            string dimensionName,
            CancellationToken cancellationToken);
    }
}
