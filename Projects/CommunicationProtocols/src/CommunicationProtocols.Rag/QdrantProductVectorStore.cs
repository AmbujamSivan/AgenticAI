using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace CommunicationProtocols.Rag;

/// <summary>
/// A Qdrant-backed <see cref="IProductVectorStore"/>. Talks to Qdrant over gRPC
/// (default port 6334). Product ids are stored as Qdrant point UUIDs so a search
/// result maps straight back to a <c>Product</c>.
/// </summary>
public sealed class QdrantProductVectorStore(QdrantClient client, string collectionName) : IProductVectorStore
{
    public async Task EnsureCollectionAsync(int dimensions, CancellationToken ct = default)
    {
        if (await client.CollectionExistsAsync(collectionName, ct))
            return;

        await client.CreateCollectionAsync(
            collectionName,
            new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine },
            cancellationToken: ct);
    }

    public async Task UpsertAsync(Guid productId, float[] embedding, CancellationToken ct = default)
    {
        var point = new PointStruct
        {
            Id = new PointId { Uuid = productId.ToString() },
            Vectors = embedding,
        };
        await client.UpsertAsync(collectionName, [point], cancellationToken: ct);
    }

    public async Task<IReadOnlyList<ProductMatch>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken ct = default)
    {
        var results = await client.QueryAsync(
            collectionName, query: queryEmbedding, limit: (ulong)topK, cancellationToken: ct);

        return results
            .Select(r => new ProductMatch(Guid.Parse(r.Id.Uuid), r.Score))
            .ToList();
    }
}
