namespace CommunicationProtocols.Rag;

/// <summary>
/// Turns text into a vector. Kept behind an interface so the local placeholder
/// embedder can be swapped for a real model (OpenAI, a local ONNX sentence model,
/// etc.) without touching the search or storage code.
/// </summary>
public interface ITextEmbedder
{
    int Dimensions { get; }
    float[] Embed(string text);
}

/// <summary>
/// Stores and searches product vectors. Implemented here with Qdrant, but the
/// interface is what the rest of the app depends on — a Pinecone or in-memory
/// implementation could replace it via DI.
/// </summary>
public interface IProductVectorStore
{
    Task EnsureCollectionAsync(int dimensions, CancellationToken ct = default);
    Task UpsertAsync(Guid productId, float[] embedding, CancellationToken ct = default);
    Task<IReadOnlyList<ProductMatch>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken ct = default);
}

/// <summary>A vector-store hit: which product, and how close it scored.</summary>
public sealed record ProductMatch(Guid ProductId, float Score);
