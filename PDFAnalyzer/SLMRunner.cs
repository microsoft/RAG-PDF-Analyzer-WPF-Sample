using Microsoft.ML.OnnxRuntimeGenAI;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PDFAnalyzer
{
    public class SLMRunner : IDisposable
    {
        private readonly string ModelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onnx-models", "phi3-directml-int4-awq-block-128");

        private Model? model = null;
        private Tokenizer? tokenizer = null;
        public event EventHandler? ModelLoaded = null;

        [MemberNotNullWhen(true, nameof(model), nameof(tokenizer))]
        public bool IsReady => model != null && tokenizer != null;

        public void Dispose()
        {
            model?.Dispose();
            tokenizer?.Dispose();
        }

        public async IAsyncEnumerable<string> InferStreamingAsync(string prompt, [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (!IsReady)
            {
                throw new InvalidOperationException("Model is not ready");
            }

            var generatorParams = new GeneratorParams(model);

            // 5.1) Tokenize the input text
            var sequences = tokenizer.Encode(prompt);

            generatorParams.SetSearchOption("max_length", 1024);
            generatorParams.SetInputSequences(sequences);
            generatorParams.TryGraphCaptureWithMaxBatchSize(1);

            using var tokenizerStream = tokenizer.CreateStream();
            using var generator = new Generator(model, generatorParams);
            StringBuilder stringBuilder = new();

            // 5.2) Generate the output text, streaming the results
            while (!generator.IsDone())
            {
                string part;
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    await Task.Delay(0, ct).ConfigureAwait(false);
                    generator.ComputeLogits();
                    generator.GenerateNextToken();

                    // 5.3) Decode the generated token
                    part = tokenizerStream.Decode(generator.GetSequence(0)[^1]);
                    stringBuilder.Append(part);
                    if (stringBuilder.ToString().Contains("<|end|>")
                        || stringBuilder.ToString().Contains("<|user|>")
                        || stringBuilder.ToString().Contains("<|system|>"))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    break;
                }

                yield return part;
            }
        }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                model = new Model(ModelDir);
                tokenizer = new Tokenizer(model);
                sw.Stop();
                Debug.WriteLine($"Model loading took {sw.ElapsedMilliseconds} ms");
                ModelLoaded?.Invoke(this, EventArgs.Empty);
            }, ct);
        }
    }
}