using System;
using System.IO;
using System.Text;
using System.IO.Compression;
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
        public void RuleDraft_CreatesPositiveNegativeAndBoundaryCases()
        {
            var cases = LocalTestCaseEngine.BuildRuleBasedDraft("로그인 기능");

            Assert.Equal(3, cases.Count);
            Assert.Contains(cases, x => x.Type == "Positive");
            Assert.Contains(cases, x => x.Type == "Negative");
            Assert.Contains(cases, x => x.Type == "Boundary");
        }

        [Fact]
        public void CsvExport_PreservesTemplateHeaderAndMapsKnownColumns()
        {
            string folder = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "tc.csv");
            try
            {
                var template = new LocalTestCaseTemplate
                {
                    Name = "custom.csv",
                    Columns = new[] { "Test Case ID", "제목", "사전조건", "테스트 절차", "기대결과", "담당자" }
                };
                var testCase = new LocalTestCase
                {
                    Id = "TC-007",
                    Title = "정상 로그인",
                    Preconditions = "계정 준비",
                    Steps = "1. ID 입력\n2. 로그인",
                    ExpectedResult = "홈 화면 표시"
                };

                LocalTestCaseEngine.ExportCsv(path, template, new[] { testCase });
                string content = File.ReadAllText(path, Encoding.UTF8);

                Assert.Contains("Test Case ID,제목,사전조건,테스트 절차,기대결과,담당자", content);
                Assert.Contains("TC-007", content);
                Assert.Contains("정상 로그인", content);
                Assert.Contains("홈 화면 표시", content);
            }
            finally
            {
                try { Directory.Delete(folder, true); } catch { }
            }
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
        [InlineData("screen.png", true)]
        [InlineData("screen.jpg", true)]
        [InlineData("legacy.ppt", false)]
        [InlineData("plan.docx", false)]
        public void PlanningDocumentReader_RecognizesSupportedFormats(string fileName, bool expected)
        {
            Assert.Equal(expected, LocalPlanningDocumentReader.IsSupported(fileName));
        }
    }
}
