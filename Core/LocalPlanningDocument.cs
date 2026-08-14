using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AppiumBuilder.Core
{
    public sealed class LocalDocumentImage
    {
        public string Name { get; init; } = string.Empty;
        public byte[] Bytes { get; init; } = Array.Empty<byte>();
    }

    public sealed class LocalPlanningDocument
    {
        public string SourcePath { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public int UnitCount { get; init; }
        public string ExtractedText { get; init; } = string.Empty;
        public IReadOnlyList<LocalDocumentImage> Images { get; init; } = Array.Empty<LocalDocumentImage>();
        public string Warning { get; init; } = string.Empty;

        public string DisplaySummary
        {
            get
            {
                string unit = Kind switch
                {
                    "PPTX" => $"{UnitCount} 슬라이드",
                    "PDF" => $"{UnitCount} 페이지",
                    _ => "이미지"
                };
                string image = Images.Count > 0 ? $" · 이미지 {Images.Count}개" : string.Empty;
                string text = !string.IsNullOrWhiteSpace(ExtractedText) ? $" · 텍스트 {ExtractedText.Length:N0}자" : string.Empty;
                string warn = !string.IsNullOrWhiteSpace(Warning) ? " · 주의" : string.Empty;
                return $"{FileName} · {Kind} · {unit}{text}{image}{warn}";
            }
        }
    }

    /// <summary>
    /// 기획서 파일을 외부 서비스 없이 로컬에서 해석 가능한 형태로 변환한다.
    /// PPTX: 슬라이드 XML 텍스트 + 포함 이미지
    /// PDF : 페이지 텍스트 + 추출 가능한 포함 이미지
    /// Image: 원본 이미지
    /// </summary>
    public static class LocalPlanningDocumentReader
    {
        private const int MaxTextCharsPerDocument = 80_000;
        private const int MaxImagesPerDocument = 8;
        private const int MaxImageBytes = 12 * 1024 * 1024;

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif"
        };

        public static bool IsSupported(string path)
        {
            string ext = Path.GetExtension(path ?? string.Empty);
            return ext.Equals(".pptx", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                || ImageExtensions.Contains(ext);
        }

        public static Task<LocalPlanningDocument> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("기획서 경로가 비어 있습니다.", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("기획서 파일을 찾을 수 없습니다.", path);
            if (!IsSupported(path)) throw new NotSupportedException("지원 형식은 PPTX, PDF, PNG/JPG/BMP/GIF 입니다.");

            return Task.Run(() => Read(path, cancellationToken), cancellationToken);
        }

        private static LocalPlanningDocument Read(string path, CancellationToken cancellationToken)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".pptx" => ReadPptx(path, cancellationToken),
                ".pdf" => ReadPdf(path, cancellationToken),
                _ => ReadImage(path, cancellationToken)
            };
        }

        private static LocalPlanningDocument ReadPptx(string path, CancellationToken cancellationToken)
        {
            var text = new StringBuilder();
            var images = new List<LocalDocumentImage>();
            int slideCount = 0;

            using ZipArchive archive = ZipFile.OpenRead(path);
            List<ZipArchiveEntry> slides = archive.Entries
                .Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                    && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => ExtractTrailingNumber(Path.GetFileNameWithoutExtension(e.Name)))
                .ToList();

            XNamespace drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
            foreach (ZipArchiveEntry slide in slides)
            {
                cancellationToken.ThrowIfCancellationRequested();
                slideCount++;
                using Stream stream = slide.Open();
                XDocument xml = XDocument.Load(stream);
                string[] fragments = xml.Descendants(drawing + "t")
                    .Select(x => (x.Value ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                if (fragments.Length > 0)
                {
                    AppendLimited(text, $"\n[슬라이드 {slideCount}]\n", MaxTextCharsPerDocument);
                    AppendLimited(text, string.Join("\n", fragments), MaxTextCharsPerDocument);
                }
            }

            foreach (ZipArchiveEntry media in archive.Entries
                .Where(e => e.FullName.StartsWith("ppt/media/", StringComparison.OrdinalIgnoreCase)
                    && ImageExtensions.Contains(Path.GetExtension(e.Name)))
                .OrderBy(e => e.FullName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (images.Count >= MaxImagesPerDocument) break;
                if (media.Length <= 0 || media.Length > MaxImageBytes) continue;

                using var ms = new MemoryStream();
                using (Stream source = media.Open()) source.CopyTo(ms);
                byte[] bytes = ms.ToArray();
                if (!LooksLikeUsefulImage(bytes, allowSmall: false)) continue;
                images.Add(new LocalDocumentImage { Name = media.Name, Bytes = bytes });
            }

            string warning = string.Empty;
            if (text.Length == 0 && images.Count == 0)
                warning = "슬라이드에서 분석 가능한 텍스트/이미지를 찾지 못했습니다.";

            return new LocalPlanningDocument
            {
                SourcePath = path,
                FileName = Path.GetFileName(path),
                Kind = "PPTX",
                UnitCount = slideCount,
                ExtractedText = text.ToString().Trim(),
                Images = images,
                Warning = warning
            };
        }

        private static LocalPlanningDocument ReadPdf(string path, CancellationToken cancellationToken)
        {
            var text = new StringBuilder();
            var images = new List<LocalDocumentImage>();
            int pageCount = 0;

            using PdfDocument document = PdfDocument.Open(path);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pageCount++;

                string pageText;
                try { pageText = ContentOrderTextExtractor.GetText(page) ?? string.Empty; }
                catch { pageText = page.Text ?? string.Empty; }

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    AppendLimited(text, $"\n[PDF 페이지 {pageCount}]\n", MaxTextCharsPerDocument);
                    AppendLimited(text, pageText.Trim(), MaxTextCharsPerDocument);
                }

                if (images.Count >= MaxImagesPerDocument) continue;
                try
                {
                    foreach (var pdfImage in page.GetImages())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (images.Count >= MaxImagesPerDocument) break;
                        if (pdfImage.WidthInSamples < 240 || pdfImage.HeightInSamples < 160) continue;

                        byte[]? bytes = null;
                        if (pdfImage.TryGetPng(out byte[]? png) && png is { Length: > 0 })
                        {
                            bytes = png;
                        }
                        else
                        {
                            byte[] raw = pdfImage.RawMemory.ToArray();
                            if (LooksLikeJpeg(raw)) bytes = raw;
                        }

                        if (bytes == null || bytes.Length == 0 || bytes.Length > MaxImageBytes) continue;
                        images.Add(new LocalDocumentImage
                        {
                            Name = $"{Path.GetFileNameWithoutExtension(path)}_p{pageCount}_img{images.Count + 1}",
                            Bytes = bytes
                        });
                    }
                }
                catch
                {
                    // 일부 PDF 이미지 포맷은 추출이 불가능할 수 있다. 텍스트 분석은 계속 진행한다.
                }
            }

            string warning = string.Empty;
            if (text.Length == 0 && images.Count == 0)
                warning = "PDF에서 텍스트/이미지를 추출하지 못했습니다. 스캔본이면 페이지를 이미지로 첨부해주세요.";
            else if (text.Length == 0)
                warning = "텍스트는 없지만 PDF 내부 이미지를 로컬 Vision 모델에 전달합니다.";

            return new LocalPlanningDocument
            {
                SourcePath = path,
                FileName = Path.GetFileName(path),
                Kind = "PDF",
                UnitCount = pageCount,
                ExtractedText = text.ToString().Trim(),
                Images = images,
                Warning = warning
            };
        }

        private static LocalPlanningDocument ReadImage(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length > MaxImageBytes)
                throw new InvalidDataException("이미지 파일이 너무 큽니다. 12MB 이하 이미지로 줄여주세요.");

            return new LocalPlanningDocument
            {
                SourcePath = path,
                FileName = Path.GetFileName(path),
                Kind = "IMAGE",
                UnitCount = 1,
                Images = new[] { new LocalDocumentImage { Name = Path.GetFileName(path), Bytes = bytes } }
            };
        }

        private static void AppendLimited(StringBuilder builder, string value, int maxChars)
        {
            if (builder.Length >= maxChars || string.IsNullOrEmpty(value)) return;
            int available = maxChars - builder.Length;
            if (value.Length <= available) builder.Append(value);
            else builder.Append(value.AsSpan(0, available));
        }

        private static int ExtractTrailingNumber(string value)
        {
            string digits = new string((value ?? string.Empty).Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            return int.TryParse(digits, out int number) ? number : int.MaxValue;
        }

        private static bool LooksLikeUsefulImage(byte[] bytes, bool allowSmall)
        {
            if (bytes.Length == 0) return false;
            if (allowSmall) return true;
            try
            {
                using var ms = new MemoryStream(bytes, writable: false);
                using Image image = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);
                return image.Width >= 240 && image.Height >= 160;
            }
            catch
            {
                return bytes.Length >= 100_000;
            }
        }

        private static bool LooksLikeJpeg(byte[] bytes)
        {
            return bytes.Length >= 4
                && bytes[0] == 0xFF && bytes[1] == 0xD8
                && bytes[^2] == 0xFF && bytes[^1] == 0xD9;
        }
    }
}
