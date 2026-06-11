using System.IO;

using QConvert.Core;

using Xunit;

namespace QConvert.Tests
{
    public sealed class OutputPathResolverTests : IDisposable
    {
        private readonly string _dir;

        public OutputPathResolverTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "QConvertTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        private string CreateFile(string name)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, "x");
            return path;
        }

        [Fact]
        public void ReplacesExtension_WhenTargetDoesNotExist()
        {
            var input = CreateFile("photo.png");

            var result = OutputPathResolver.GetUniquePath(input, ".jpg");

            Assert.Equal(Path.Combine(_dir, "photo.jpg"), result);
        }

        [Fact]
        public void InsertsNumericSuffix_WhenTargetExists()
        {
            var input = CreateFile("photo.png");
            CreateFile("photo.jpg");

            var result = OutputPathResolver.GetUniquePath(input, ".jpg");

            Assert.Equal(Path.Combine(_dir, "photo.001.jpg"), result);
        }

        [Fact]
        public void SkipsToNextFreeSuffix_WhenSuffixedNamesExist()
        {
            var input = CreateFile("photo.png");
            CreateFile("photo.jpg");
            CreateFile("photo.001.jpg");
            CreateFile("photo.002.jpg");

            var result = OutputPathResolver.GetUniquePath(input, ".jpg");

            Assert.Equal(Path.Combine(_dir, "photo.003.jpg"), result);
        }

        [Fact]
        public void PreservesDotsInBaseName()
        {
            var input = CreateFile("my.holiday.photo.webp");

            var result = OutputPathResolver.GetUniquePath(input, ".png");

            Assert.Equal(Path.Combine(_dir, "my.holiday.photo.png"), result);
        }

        [Fact]
        public void Throws_WhenAllSuffixesAreTaken()
        {
            var input = CreateFile("photo.png");
            CreateFile("photo.jpg");
            for (var i = 1; i <= 999; i++)
            {
                CreateFile($"photo.{i:000}.jpg");
            }

            Assert.Throws<IOException>(() => OutputPathResolver.GetUniquePath(input, ".jpg"));
        }
    }
}
