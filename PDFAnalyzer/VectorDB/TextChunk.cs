namespace PDFAnalyzer.VectorDB
{
    public class TextChunk
    {
        public TextChunk()
        {
            Page = 0;
            Text = null;
            Vectors = Array.Empty<float>();
        }

        public TextChunk(TextChunk textChunk)
        {
            Page = textChunk.Page;
            Text = textChunk.Text;
            Vectors = textChunk.Vectors;
        }

        public int Page { get; set; }
        public string? Text { get; set; }
        public float[] Vectors { get; set; }
    }
}
