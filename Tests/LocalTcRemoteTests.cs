using System.Threading.Tasks;
using AppiumBuilder.Core;
using Xunit;

namespace AppiumBuilder.Tests
{
    public sealed class LocalTcRemoteTests
    {
        [Theory]
        [InlineData("http://127.0.0.1:7788", true)]
        [InlineData("http://10.10.20.30:7788", true)]
        [InlineData("http://172.16.1.10:7788", true)]
        [InlineData("http://172.31.255.254:7788", true)]
        [InlineData("http://192.168.0.50:7788", true)]
        [InlineData("http://169.254.1.1:7788", true)]
        [InlineData("http://8.8.8.8:7788", false)]
        [InlineData("https://1.1.1.1:7788", false)]
        public async Task RemoteEndpoint_AllowsOnlyLoopbackOrPrivateLan(string endpoint, bool expected)
        {
            var result = await LocalTcRemoteClient.ValidateIntranetEndpointAsync(endpoint);
            Assert.Equal(expected, result.Allowed);
        }
    }
}
