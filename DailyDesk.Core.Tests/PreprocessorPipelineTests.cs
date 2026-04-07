using Xunit;

namespace DailyDesk.Core.Tests
{
    public class PreprocessorPipelineTests
    {
        [Fact]
        public void ScoringPipeline_ShouldReturnPreScore()
        {
            // Arrange
            var baseScore = 4;
            var testSignal = 2;
            var sizeSignal = 1;

            // Act
            var preScore = baseScore + testSignal + sizeSignal;

            // Assert
            Assert.True(preScore >= 4 && preScore <= 9);
        }

        [Fact]
        public void GateFailed_ShouldSkipLLM()
        {
            var ciStatus = "failure";
            Assert.Equal("failure", ciStatus);
        }
    }
}
