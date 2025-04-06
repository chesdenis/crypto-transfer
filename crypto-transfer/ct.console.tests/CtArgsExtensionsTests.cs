using ct.console.common;
using ct.console.model;

namespace ct.console.tests
{
    public class CtArgsExtensionsTests
    {
        [Fact]
        public void GetFileExtensionFilter_ShouldReturnSpecifiedFileExtension()
        {
            // Arrange
            var args = new[] { "--file-ext=.txt" };

            // Act
            var result = args.GetFileExtensionFilter();

            // Assert
            Assert.Equal(".txt", result);
        }

        [Fact]
        public void GetFileExtensionFilter_ShouldReturnDefaultWhenNotSpecified()
        {
            // Arrange
            var args = Array.Empty<string>();

            // Act
            var result = args.GetFileExtensionFilter();

            // Assert
            Assert.Equal("*.*", result);
        }

        [Fact]
        public void GetMode_ShouldReturnSpecifiedMode()
        {
            // Arrange
            var args = new[] { "--mode=Combiner" };

            // Act
            var result = args.GetMode();

            // Assert
            Assert.Equal(CtMode.Combiner, result);
        }

        [Fact]
        public void GetMode_ShouldReturnDefaultModeWhenNotSpecified()
        {
            // Arrange
            var args = Array.Empty<string>();

            // Act
            var result = args.GetMode();

            // Assert
            Assert.Equal(CtMode.Server, result);
        }

        [Fact]
        public void GetChunkSize_ShouldReturnSpecifiedChunkSize()
        {
            // Arrange
            var args = new[] { "--chunk-size=5242880" };

            // Act
            var result = args.GetChunkSize();

            // Assert
            Assert.Equal(5242880, result);
        }

        [Fact]
        public void GetChunkSize_ShouldReturnDefaultChunkSizeWhenNotSpecified()
        {
            // Arrange
            var args = Array.Empty<string>();

            // Act
            var result = args.GetChunkSize();

            // Assert
            Assert.Equal(1024 * 1024 * 10, result);
        }

        [Fact]
        public void GetThreadsCount_ShouldReturnSpecifiedThreadCount()
        {
            // Arrange
            var args = new[] { "--threads=8" };

            // Act
            var result = args.GetThreadsCount();

            // Assert
            Assert.Equal(8, result);
        }

        [Fact]
        public void GetThreadsCount_ShouldReturnDefaultThreadCountWhenNotSpecified()
        {
            // Arrange
            var args = Array.Empty<string>();

            // Act
            var result = args.GetThreadsCount();

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public void GetChunkMapPath_ShouldReturnSpecifiedChunkMapPath()
        {
            // Arrange
            var args = new[] { "--chunk-map=/path/to/chunkMap.json" };

            // Act
            var result = args.GetChunkMapPath();

            // Assert
            Assert.Equal("/path/to/chunkMap.json", result);
        }

        [Fact]
        public void GetChunkMapPath_ShouldReturnDefaultChunkMapPathWhenNotSpecified()
        {
            // Arrange
            var args = Array.Empty<string>();
            var expectedPath = Path.Combine(AppContext.BaseDirectory, "chunkMap.json");

            // Act
            var result = args.GetChunkMapPath();

            // Assert
            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void GetServerUrl_ShouldReturnSpecifiedServerUrl()
        {
            // Arrange
            var args = new[] { "--server-url=http://example.com" };

            // Act
            var result = args.GetServerUrl();

            // Assert
            Assert.Equal("http://example.com", result);
        }

        [Fact]
        public void GetServerUrl_ShouldThrowExceptionWhenNotSpecified()
        {
            // Arrange
            var args = Array.Empty<string>();

            // Act + Assert
            var exception = Assert.Throws<ArgumentException>(() => args.GetServerUrl());
            Assert.Equal("Server url is not specified", exception.Message);
        }
    }
}