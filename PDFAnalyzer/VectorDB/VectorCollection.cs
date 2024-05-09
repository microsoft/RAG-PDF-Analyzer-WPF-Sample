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
                var score = CosineSimilarity(TextChunks[i].Vectors, searchVector);
                scores[i] = score;
            }

            var indexedFloats = scores.Select((value, index) => new { Value = value, Index = index })
              .ToArray();

            // Sort the indexed floats by value in descending order
            Array.Sort(indexedFloats, (a, b) => b.Value.CompareTo(a.Value));

            // Extract the top k indices
            indexRanks = indexedFloats.Select(item => item.Index).ToArray();

            return indexRanks;
        }

        private static float CosineSimilarity(float[] v1, float[] v2)
        {
            if (v1.Length != v2.Length)
            {
                throw new ArgumentException("Vectors must have the same length.");
            }
            return DotProduct(v1, v2);
        }

        private static float CheckOverflow(double x)
        {
            if (x >= double.MaxValue)
            {
                throw new OverflowException("operation caused overflow");
            }
            return (float)x;
        }

        private static float DotProduct(float[] a, float[] b)
        {
            float result = 0.0f;
            for (int i = 0; i < a.Length; i++)
            {
                result = CheckOverflow(result + CheckOverflow(a[i] * b[i]));
            }
            return result;
        }
    }
}
