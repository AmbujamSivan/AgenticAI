using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qdrant.Client;

namespace CommunicationProtocols.Rag;

/// <summary>DI wiring for the RAG stack. One call registers the embedder, the Qdrant
/// vector store, the search service, and a hosted indexer that fills the store on
/// startup. Swapping providers (embedder or vector store) is a change here only.</summary>
public static class RagServiceCollectionExtensions
{
    public static IServiceCollection AddProductRag(
        this IServiceCollection services,
        string qdrantHost = "localhost",
        int qdrantPort = 6334,
        string collectionName = "products",
        int dimensions = 256)
    {
        services.AddSingleton<ITextEmbedder>(new HashingTextEmbedder(dimensions));
        services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));
        services.AddSingleton<IProductVectorStore>(sp =>
            new QdrantProductVectorStore(sp.GetRequiredService<QdrantClient>(), collectionName));
        services.AddSingleton<ProductSearchService>();
        services.AddHostedService<ProductIndexer>();
        return services;
    }
}

/// <summary>Indexes all products into the vector store when the app starts.</summary>
internal sealed class ProductIndexer(ProductSearchService search, ILogger<ProductIndexer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await search.IndexAllAsync(cancellationToken);
        logger.LogInformation("RAG: products indexed into the vector store.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
