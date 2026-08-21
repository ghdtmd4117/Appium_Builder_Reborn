using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppiumBuilder.Core
{
    public sealed class LocalTcServerHealth
    {
        public bool Ready { get; set; }
        public string Model { get; set; } = string.Empty;
        public string ModelDisplayName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int QueueDepth { get; set; }
    }

    public sealed class RemoteLearningResult
    {
        public TcLearningDigest Digest { get; set; } = new();
        public List<Dictionary<string, string>> RepresentativeExamples { get; set; } = new();
        public List<string> SourceNames { get; set; } = new();
    }

    public sealed class LocalTcRemoteClient : IDisposable
    {
        private readonly HttpClient _client;
        private readonly string _endpoint;
        private readonly string _token;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public LocalTcRemoteClient(string endpoint, string token)
        {
            _endpoint = NormalizeEndpoint(endpoint);
            _token = (token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(_token))
                throw new InvalidOperationException("사내 AI 서버 연결 토큰을 입력해주세요.");

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false
            };
            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(12)
            };
            _client.DefaultRequestHeaders.Add("X-Local-TC-Token", _token);
        }

        public static async Task<(bool Allowed, string Message)> ValidateIntranetEndpointAsync(
            string endpoint,
            CancellationToken cancellationToken = default)
        {
            if (!Uri.TryCreate(NormalizeEndpoint(endpoint), UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return (false, "서버 주소는 http:// 또는 https:// 형식이어야 합니다.");

            if (uri.IsLoopback) return (true, string.Empty);

            try
            {
                IPAddress[] addresses;
                if (IPAddress.TryParse(uri.Host, out IPAddress? literal))
                    addresses = new[] { literal };
                else
                    addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken).ConfigureAwait(false);

                if (addresses.Length == 0)
                    return (false, "서버 주소의 IP를 확인하지 못했습니다.");

                if (addresses.All(IsPrivateOrLocalAddress))
                    return (true, string.Empty);

                return (false, "보안상 Local TC Server는 localhost 또는 사설/사내 네트워크 주소에만 연결할 수 있습니다.");
            }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                return (false, "서버 주소를 확인하지 못했습니다: " + ex.Message);
            }
        }

        public async Task<LocalTcServerHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            await EnsureEndpointAllowedAsync(cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint + "/api/health");
            using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, body, "사내 AI 서버 상태 확인");
            return JsonSerializer.Deserialize<LocalTcServerHealth>(body, JsonOptions)
                ?? throw new InvalidDataException("사내 AI 서버 상태 응답을 해석하지 못했습니다.");
        }

        public async Task<RemoteLearningResult> LearnProfileAsync(
            string manualRules,
            IReadOnlyList<string> examplePaths,
            IReadOnlyList<string> documentPaths,
            CancellationToken cancellationToken = default)
        {
            await EnsureEndpointAllowedAsync(cancellationToken).ConfigureAwait(false);
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(manualRules ?? string.Empty, Encoding.UTF8), "manualRules");
            AddFiles(form, "examples", examplePaths);
            AddFiles(form, "documents", documentPaths);

            using HttpResponseMessage response = await _client.PostAsync(_endpoint + "/api/learn", form, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, body, "사내 AI 서버 프로필 학습");
            return JsonSerializer.Deserialize<RemoteLearningResult>(body, JsonOptions)
                ?? throw new InvalidDataException("사내 AI 서버 학습 응답을 해석하지 못했습니다.");
        }

        public async Task<GeneratedTcBatch> GenerateAsync(
            string requirement,
            TcLearningProfile profile,
            IReadOnlyList<string> documentPaths,
            CancellationToken cancellationToken = default)
        {
            await EnsureEndpointAllowedAsync(cancellationToken).ConfigureAwait(false);
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(requirement ?? string.Empty, Encoding.UTF8), "requirement");
            string profileJson = JsonSerializer.Serialize(profile);
            form.Add(new StringContent(profileJson, Encoding.UTF8, "application/json"), "profileJson");
            AddFiles(form, "documents", documentPaths);

            using HttpResponseMessage response = await _client.PostAsync(_endpoint + "/api/generate", form, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response, body, "사내 AI 서버 TC 생성");

            RemoteGeneratedBatch? dto = JsonSerializer.Deserialize<RemoteGeneratedBatch>(body, JsonOptions);
            if (dto == null) throw new InvalidDataException("사내 AI 서버 생성 응답을 해석하지 못했습니다.");
            return new GeneratedTcBatch
            {
                Columns = dto.Columns ?? Array.Empty<string>(),
                Cases = (dto.Cases ?? new List<Dictionary<string, string>>())
                    .Select(x => new DynamicTestCase { Fields = new Dictionary<string, string>(x, StringComparer.CurrentCultureIgnoreCase) })
                    .ToArray()
            };
        }

        private async Task EnsureEndpointAllowedAsync(CancellationToken cancellationToken)
        {
            var validation = await ValidateIntranetEndpointAsync(_endpoint, cancellationToken).ConfigureAwait(false);
            if (!validation.Allowed) throw new InvalidOperationException(validation.Message);
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            string value = (endpoint ?? string.Empty).Trim().TrimEnd('/');
            return string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:7788" : value;
        }

        private static bool IsPrivateOrLocalAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)) return true;
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] b = address.GetAddressBytes();
                return b[0] == 10
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                    || (b[0] == 192 && b[1] == 168)
                    || (b[0] == 169 && b[1] == 254);
            }
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;
                byte first = address.GetAddressBytes()[0];
                return (first & 0xFE) == 0xFC;
            }
            return false;
        }

        private static void AddFiles(MultipartFormDataContent form, string fieldName, IReadOnlyList<string> paths)
        {
            foreach (string path in (paths ?? Array.Empty<string>()).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
                var content = new StreamContent(stream, 1024 * 1024);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(content, fieldName, Path.GetFileName(path));
            }
        }

        private static void EnsureSuccess(HttpResponseMessage response, string body, string operation)
        {
            if ((int)response.StatusCode is >= 300 and < 400)
                throw new InvalidOperationException(operation + " 중 Redirect 응답을 받아 중단했습니다.");
            if (response.IsSuccessStatusCode) return;

            string message = body;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out JsonElement error))
                    message = error.GetString() ?? body;
                else if (doc.RootElement.TryGetProperty("detail", out JsonElement detail))
                    message = detail.GetString() ?? body;
            }
            catch { }
            if (message.Length > 900) message = message[..900] + "…";
            throw new InvalidOperationException($"{operation} 실패 ({(int)response.StatusCode}): {message}");
        }

        public void Dispose() => _client.Dispose();

        private sealed class RemoteGeneratedBatch
        {
            public string[]? Columns { get; set; }
            public List<Dictionary<string, string>>? Cases { get; set; }
        }
    }
}
