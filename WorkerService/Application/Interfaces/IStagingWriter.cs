using DataOpinionsExtractor.Domain;

namespace DataOpinionsExtractor.Application.Interfaces;

public interface IStagingWriter
{
    Task<int> WriteAsync(string sourceName, ExtractedDataset dataset, CancellationToken cancellationToken);
}