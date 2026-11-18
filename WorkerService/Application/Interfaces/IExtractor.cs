using DataOpinionsExtractor.Domain;

namespace DataOpinionsExtractor.Application.Interfaces;

public interface IExtractor
{
    string Name { get; }

    int MaxParallelism { get; }

  
    Task<IReadOnlyList<ExtractedDataset>> GetDatasetsAsync(CancellationToken cancellationToken);
}