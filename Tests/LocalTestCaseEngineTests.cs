using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppiumBuilder.Core;
using Xunit;

namespace AppiumBuilder.Tests
{
    public sealed class LocalTestCaseEngineTests
    {
        [Theory]
        [InlineData("http://127.0.0.1:11434", true)]
        [InlineData("http://localhost:11434", true)]
        [InlineData("http://[::1]:11434", true)]
        [InlineData("https://example.com", false)]
        [InlineData("http://192.168.0.10:11434", false)]
        [InlineData("not-a-url", false)]
        public void LocalLlmEndpoint_AllowsOnlyLoopback(string endpoint, bool expected)
        {
            Assert.Equal(expected, LocalOnlyLlmClient.IsLoopbackEndpoint(endpoint));
        }

        [Fact]
        public void EmbeddedLocalAiRuntime_UsesPinnedOfficialAndLoopbackConfiguration()
        {
            var endpoint = new Uri(LocalAiRuntimeManager.Endpoint);
            Assert.True(endpoint.IsLoopback);
            Assert.Equal("127.0.0.1", endpoint.Host);
            Assert.Equal(11434, endpoint.Port);
            Assert.Equal("github.com", LocalAiRuntimeManager.RuntimeDownloadUri.Host);
            Assert.Equal(Uri.UriSchemeHttps, LocalAiRuntimeManager.RuntimeDownloadUri.Scheme);
            Assert.Equal(64, LocalAiRuntimeManager.RuntimeSha256.Length);
            Assert.Equal("qwen3-vl:4b", LocalAiRuntimeManager.DefaultModel);
        }

        [Fact]
        public void ExistingTcCsv_LearnsArbitraryColumnsWithoutFixedSchema()
        {
            string folder = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "team_tc.csv");
            try
            {
                File.WriteAllText(path,
                    "Case No,기능분류,검증 포인트,Result Note\r\n" +
                    "A-001,로그인,정상 계정으로 로그인,홈 진입\r\n" +
                    "A-002,로그인,잠금 계정 로그인,잠금 안내\r\n",
                    new UTF8Encoding(true));

                TcExampleSet set = LocalTestCaseEngine.ReadExampleSet(path);

                Assert.Equal(new[] { "Case No", "기능분류", "검증 포인트", "Result Note" }, set.Columns);
                Assert.Equal(2, set.TotalRowCount);
                Assert.Equal("A-001", set.Rows[0]["Case No"]);
                Assert.DoesNotContain("사전조건", set.Columns);
                Assert.DoesNotContain("우선순위", set.Columns);
            }
            finally
            {
                try { Directory.Delete(folder, true); } catch { }
            }
        }

        [Fact]
        public void CsvExport_PreservesDynamicProjectColumnsExactly()
        {
            string folder = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "tc.csv");
            try
            {
                string[] columns = { "No", "시나리오", "체크사항", "비고" };
                var row = new DynamicTestCase
                {
                    Fields = new Dictionary<string, string>
                    {
                        ["No"] = "1",
                        ["시나리오"] = "회원가입",
                        ["체크사항"] = "약관 동의 후 완료",
                        ["비고"] = "모바일"
                    }
                };

                LocalTestCaseEngine.ExportCsv(path, columns, new[] { row });
                string content = File.ReadAllText(path, Encoding.UTF8);

                Assert.Contains("No,시나리오,체크사항,비고", content);
                Assert.Contains("회원가입", content);
                Assert.DoesNotContain("우선순위", content);
            }
            finally
            {
                try { Directory.Delete(folder, true); } catch { }
            }
        }

        [Fact]
        public void CanonicalColumns_PrefersMostCommonObservedProjectSchema()
        {
            var a = new TcExampleSet { Columns = new[] { "A", "B", "C" }, TotalRowCount = 10 };
            var b = new TcExampleSet { Columns = new[] { "X", "Y" }, TotalRowCount = 20 };
            var c = new TcExampleSet { Columns = new[] { "A", "B", "C" }, TotalRowCount = 5 };

            IReadOnlyList<string> columns = LocalTestCaseEngine.ChooseCanonicalColumns(new[] { a, b, c });

            Assert.Equal(new[] { "A", "B", "C" }, columns);
        }

        [Fact]
        public async Task PlanningDocumentReader_ExtractsPptxSlideTextLocally()
        {
            string folder = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "plan.pptx");
            try
            {
                using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = zip.CreateEntry("ppt/slides/slide1.xml");
                    await using Stream stream = entry.Open();
                    await using var writer = new StreamWriter(stream, Encoding.UTF8);
                    await writer.WriteAsync("<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>로그인 화면</a:t></a:r></a:p><a:p><a:r><a:t>비밀번호 5회 실패 시 잠금</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>");
                }

                LocalPlanningDocument document = await LocalPlanningDocumentReader.ReadAsync(path);
                Assert.Equal("PPTX", document.Kind);
                Assert.Equal(1, document.UnitCount);
                Assert.Contains("로그인 화면", document.ExtractedText);
                Assert.Contains("5회 실패", document.ExtractedText);
            }
            finally
            {
                try { Directory.Delete(folder, true); } catch { }
            }
        }

        [Theory]
        [InlineData("plan.pptx", true)]
        [InlineData("plan.pdf", true)]
        [InlineData("guide.docx", true)]
        [InlineData("rules.txt", true)]
        [InlineData("rules.md", true)]
        [InlineData("screen.png", true)]
        [InlineData("screen.jpg", true)]
        [InlineData("legacy.ppt", false)]
        public void PlanningDocumentReader_RecognizesSupportedFormats(string fileName, bool expected)
        {
            Assert.Equal(expected, LocalPlanningDocumentReader.IsSupported(fileName));
        }
    }
}
