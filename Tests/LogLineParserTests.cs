using AppiumBuilder.Core;
using Xunit;

namespace AppiumBuilder.Tests
{
    public class LogLineParserTests
    {
        [Theory]
        [InlineData("05-13 14:35:06.245  2345  2345 D example.app: hello", "D")]
        [InlineData("14:35:06.512 2345 2345 I example.app: ok", "I")]
        [InlineData("[adb] daemon started", "")]
        public void GetLevel_ParsesCommonLogcatFormats(string line, string expected)
        {
            Assert.Equal(expected, LogLineParser.GetLevel(line));
        }

        [Fact]
        public void Matches_AppliesLevelAndTextTogether()
        {
            string line = "05-13 14:35:06.245 2345 2345 D example.app: User login success";
            Assert.True(LogLineParser.Matches(line, "D", "login"));
            Assert.False(LogLineParser.Matches(line, "E", "login"));
            Assert.False(LogLineParser.Matches(line, "D", "payment"));
        }
    }
}
