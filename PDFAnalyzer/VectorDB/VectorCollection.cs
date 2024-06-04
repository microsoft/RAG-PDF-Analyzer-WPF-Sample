using System.Numerics.Tensors;

namespace PDFAnalyzer.VectorDB
{
    public class VectorCollection
    {
        public VectorCollection()
        {
            Dimensions = 0;
            TextChunks = [];
        }

        public VectorCollection(int dimensions, List<TextChunk>? objects = null)
        {
            Dimensions = dimensions;
            TextChunks = objects ?? [];
        }

        public int Dimensions { get; set; }

        public List<TextChunk> TextChunks { get; set; }

        public int[] CalculateRanking(float[] searchVector)
        {
            float[] scores = new float[Dimensions];
            int[] indexRanks = new int[Dimensions];

            for (int i = 0; i < Dimensions; i++)
            {
                scores[i] = TensorPrimitives.CosineSimilarity(TextChunks[i].Vectors, searchVector);
            }

            var indexedFloats = scores.Select((value, index) => new { Value = value, Index = index })
              .ToArray();

            // Sort the indexed floats by value in descending order
            Array.Sort(indexedFloats, (a, b) => b.Value.CompareTo(a.Value));

            // Extract the top k indices
            indexRanks = indexedFloats.Select(item => item.Index).ToArray();

            return indexRanks;
        }
    }
}
