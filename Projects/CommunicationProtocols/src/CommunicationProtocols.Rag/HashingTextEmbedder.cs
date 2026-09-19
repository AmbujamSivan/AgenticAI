using System.Text;

namespace CommunicationProtocols.Rag;

/// <summary>
/// A dependency-free, deterministic placeholder embedder: it hashes each word into a
/// fixed-size vector (signed "hashing trick") and L2-normalizes the result, so cosine
/// similarity reflects shared vocabulary. This is NOT real semantic search — "laptop"
/// and "notebook" won't match — but it exercises the whole RAG pipeline with zero
/// external services. Swap in a real <see cref="ITextEmbedder"/> for true semantics.
/// </summary>
public sealed class HashingTextEmbedder(int dimensions = 256) : ITextEmbedder
{
    public int Dimensions { get; } = dimensions;

    public float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        foreach (var token in Tokenize(text))
        {
            var hash = Fnv1a(token);
            var index = (int)(hash % (uint)Dimensions);
            var sign = ((hash >> 31) & 1) == 0 ? 1f : -1f; // signed hashing limits collisions
            vector[index] += sign;
        }
        Normalize(vector);
        return vector;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (sb.Length > 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }
        if (sb.Length > 0)
            yield return sb.ToString();
    }

    private static uint Fnv1a(string token)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var b in Encoding.UTF8.GetBytes(token))
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }

    private static void Normalize(float[] vector)
    {
        double sumSq = 0;
        foreach (var v in vector)
            sumSq += v * (double)v;

        if (sumSq <= 0)
            return;

        var inv = (float)(1.0 / Math.Sqrt(sumSq));
        for (var i = 0; i < vector.Length; i++)
            vector[i] *= inv;
    }
}
