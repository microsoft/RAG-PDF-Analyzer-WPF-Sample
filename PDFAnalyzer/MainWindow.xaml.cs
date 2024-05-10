using PDFAnalyzer.VectorDB;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UglyToad.PdfPig;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PDFAnalyzer
{
    public sealed partial class MainWindow : Window
    {
        private readonly SLMRunner SLMRunner;
        private readonly RAGService RAGService;
        private CancellationTokenSource? cts;
        private List<uint>? selectedPages = null;
        private int selectedPageIndex = -1;
        private StorageFile? pdfFile;

        [GeneratedRegex(@"[\u0000-\u001F\u007F-\uFFFF]")]
        private static partial Regex MyRegex();

        public MainWindow()
        {
            SLMRunner = new SLMRunner();
            SLMRunner.ModelLoaded += (sender, e) => Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => CheckIfReady(sender));

            RAGService = new RAGService();
            RAGService.ResourcesLoaded += (sender, e) => Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => CheckIfReady(sender));

            Closed += (sender, e) =>
            {
                cts?.Cancel();
                cts = null;
                SLMRunner?.Dispose();
                RAGService?.Dispose();
            };

            InitializeComponent();
        }

        private void CheckIfReady(object? sender)
        {
            if (sender == RAGService)
            {
                IndexPDFButton.IsEnabled = RAGService.IsModelReady;
            }

            AskSLMButton.IsEnabled = SLMRunner.IsReady && RAGService.IsReady;
            if (RAGService.IsReady)
            {
                IndexPDFButton.Content = "Model Ready";
            }
        }

        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => Task.WhenAll(SLMRunner.InitializeAsync(), RAGService.InitializeAsync()));
        }

        private async void IndexPDFButton_Click(object sender, RoutedEventArgs e)
        {
            IndexPDFButton.IsEnabled = false;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".pdf",
                Filter = "Pdf documents (.pdf)|*.pdf"
            };

            bool? result = dialog.ShowDialog();

            if (result != true)
            {
                IndexPDFButton.IsEnabled = RAGService.IsModelReady;
                return;
            }

            IndexPDFProgressStackPanel.Visibility = Visibility.Visible;
            IndexPDFProgressBar.Minimum = 0;
            IndexPDFProgressBar.Maximum = 1;
            IndexPDFProgressBar.Value = 0;
            IndexPDFProgressTextBlock.Text = "Reading PDF...";

            ShowPDFPage.IsEnabled = true;
            Title = $"RAG with Phi 3 - {Path.GetFileName(dialog.FileName)}";
            pdfFile = await StorageFile.GetFileFromPathAsync(dialog.FileName).AsTask().ConfigureAwait(false);

            var contents = new List<TextChunk>();
            using (PdfDocument document = PdfDocument.Open(pdfFile.Path))
            {
                foreach (var page in document.GetPages())
                {
                    var words = page.GetWords();
                    var builder = string.Join(" ", words);

                    var range = builder
                            .Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => MyRegex().Replace(x, ""))
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => new TextChunk
                            {
                                Text = x,
                                Page = page.Number,
                            });

                    contents.AddRange(range);
                }
            }

            var maxLength = 1024 / 4;
            for (int i = 0; i < contents.Count; i++)
            {
                var content = contents[i];
                int index = 0;
                var contentChunks = new List<TextChunk>();
                while (index < content.Text!.Length)
                {
                    if (index + maxLength >= content.Text.Length)
                    {
                        contentChunks.Add(new TextChunk(content)
                        {
                            Text = Regex.Replace(content.Text[index..].Trim(), @"(\.){2,}", ".")
                        });
                        break;
                    }

                    int lastIndexOfBreak = content.Text.LastIndexOf(' ', index + maxLength, maxLength);
                    if (lastIndexOfBreak <= index)
                    {
                        lastIndexOfBreak = index + maxLength;
                    }

                    contentChunks.Add(new TextChunk(content)
                    {
                        Text = Regex.Replace(content.Text[index..lastIndexOfBreak].Trim(), @"(\.){2,}", ".")
                    }); ;

                    index = lastIndexOfBreak + 1;
                }

                contents.RemoveAt(i);
                contents.InsertRange(i, contentChunks);
                i += contentChunks.Count - 1;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IndexPDFProgressBar.Minimum = 0;
                IndexPDFProgressBar.Maximum = contents.Count;
                IndexPDFProgressBar.Value = 0;
            });

            Stopwatch sw = Stopwatch.StartNew();

            void UpdateProgress(float progress)
            {
                var elapsed = sw.Elapsed;
                if (progress == 0)
                {
                    progress = 0.0001f;
                }

                var remaining = TimeSpan.FromSeconds((long)(elapsed.TotalSeconds / progress * (1 - progress) / 5) * 5);

                IndexPDFProgressBar.Value = progress * contents.Count;
                IndexPDFProgressTextBlock.Text = $"Indexing PDF... {progress:P0} ({remaining})";
            }

            if (cts != null)
            {
                cts.Cancel();
                cts = null;
                AskSLMButton.Content = "Answer";
                return;
            }

            cts = new CancellationTokenSource();

            await RAGService.InitializeAsync(contents, (sender, progress) =>
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
                {
                    UpdateProgress(progress);
                });
            }, cts.Token);

            cts = null;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IndexPDFProgressTextBlock.Text = "Indexing PDF... Done!";
            });

            await Task.Delay(1000);

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                IndexPDFProgressStackPanel.Visibility = Visibility.Collapsed;
                IndexPDFButton.IsEnabled = RAGService.IsModelReady;
                await Task.Delay(1000);
                IndexPDFGrid.Visibility = Visibility.Collapsed;
                ChatGrid.Visibility = Visibility.Visible;
            });
        }

        private async void AskSLMButton_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts = null;
                AskSLMButton.Content = "Answer";
                return;
            }

            var prompt = """
        <|system|>
        You are a helpful assistant helping answer questions about this information:
        """;

            cts = new CancellationTokenSource();
            AskSLMButton.Content = "Cancel";

            SLMRunner.SearchMaxLength = Math.Min(4096, Math.Max(1024, (int)(RAGService.MaxDedicatedVideoMemory / (1024 * 1024))));

            List<TextChunk> contents = (await RAGService.Search(SearchTextBox.Text, 3, 1)).OrderBy(c => c.ChunkIndexInSource).ToList();

            selectedPages = contents.Select(c => (uint)c.Page).Distinct().ToList();
            selectedPageIndex = 0;

            PagesUsedRun.Text = $"Using page(s) : {string.Join(", ", selectedPages)}";

            var pagesChunks = contents.GroupBy(c => c.Page).Select(g => new { Page = g.Key, Text = string.Join(Environment.NewLine, g.OrderBy(g => g.ChunkIndexInSource).Select(c => c.Text)) }).ToList();

            prompt += string.Join(Environment.NewLine, pagesChunks.Select(c => $"{Environment.NewLine}Page {c.Page}: {c.Text}"));

            prompt += $"""
        <|end|>
        <|user|>
        {SearchTextBox.Text}<|end|>
        <|assistant|>
        """;

            AnswerRun.Text = "";
            var fullResult = "";

            await Task.Run(async () =>
            {
                await foreach (var partialResult in SLMRunner.InferStreamingAsync(prompt).WithCancellation(cts.Token))
                {
                    fullResult += partialResult;
                    await Application.Current.Dispatcher.InvokeAsync(() => AnswerRun.Text = fullResult);
                }
            }, cts.Token);

            cts = null;

            AskSLMButton.Content = "Answer";
        }

        private async void ShowPDFPage_Click(object sender, RoutedEventArgs e)
        {
            await UpdatePdfImageAsync().ConfigureAwait(false);
        }

        private async Task UpdatePdfImageAsync()
        {
            if (pdfFile == null || selectedPages == null || selectedPages.Count() == 0)
            {
                return;
            }

            var pdfDocument = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(pdfFile).AsTask().ConfigureAwait(false);
            var pageId = selectedPages[selectedPageIndex];
            if (pageId < 0 || pdfDocument.PageCount < pageId)
            {
                return;
            }
            var page = pdfDocument.GetPage(pageId - 1);
            InMemoryRandomAccessStream inMemoryRandomAccessStream = new();
            var rect = page.Dimensions.TrimBox;
            await page.RenderToStreamAsync(inMemoryRandomAccessStream).AsTask().ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                BitmapImage bitmapImage = new();

                bitmapImage.BeginInit();
                bitmapImage.StreamSource = inMemoryRandomAccessStream.AsStream();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                PdfImage.Source = bitmapImage;
                PageNumberTextBlock.Text = $"{pageId}/{pdfDocument.PageCount}";

                PdfImageGrid.Visibility = Visibility.Visible;
                UpdatePreviousAndNextPageButtonEnabled();
            });
        }

        private void UpdatePreviousAndNextPageButtonEnabled()
        {
            if (selectedPages == null || selectedPages.Count == 0)
            {
                PreviousPageButton.IsEnabled = false;
                NextPageButton.IsEnabled = false;
                return;
            }

            PreviousPageButton.IsEnabled = selectedPageIndex > 0;
            NextPageButton.IsEnabled = selectedPageIndex < selectedPages.Count - 1;
        }

        private void PdfImage_Tapped(object sender, MouseButtonEventArgs e)
        {
            PdfImageGrid.Visibility = Visibility.Collapsed;
        }

        private async void PreviousPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPageIndex <= 0)
            {
                return;
            }
            selectedPageIndex--;
            await UpdatePdfImageAsync().ConfigureAwait(false);
        }

        private async void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPages == null || selectedPageIndex >= selectedPages.Count - 1)
            {
                return;
            }
            selectedPageIndex++;
            await UpdatePdfImageAsync().ConfigureAwait(false);
        }
    }
}
