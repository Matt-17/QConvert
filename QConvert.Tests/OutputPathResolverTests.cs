using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using QConvert.Core;

namespace QConvert.Tests
{
    [TestClass]
    public sealed class OutputPathResolverTests
    {
        private readonly string _dir;

        public OutputPathResolverTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "QConvertTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void Cleanup() => Directory.Delete(_dir, recursive: true);

        private string CreateFile(string name)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, "x");
            return path;
        }

        [TestMethod]
        public void ReplacesExtension_WhenTargetDoesNotExist()
        {
            var input = CreateFile("photo.png");

            var result = OutputPathResolver.GetUniquePath(input, ".jpg");

            Assert.AreEqual(Path.Combine(_dir, "photo.jpg"), result);
        }

        [TestMethod]
        public void InsertsNumericSuffix_WhenTargetExists()
        {
            var input = CreateFile("photo.png");
            CreateFile("photo.jpg");

            var result = OutputPathResolver.GetUniquePath(input, ".jpg");

            Assert.AreEqual(Path.Combine(_dir, "photo.001.jpg"), result);
        }

        [TestMethod]
        public void SkipsToNextFreeSuffix_WhenSuffixedNamesExist()
        {
            var input = CreateFile("photo.png");
            CreateFile("photo.jpg");
            CreateFile("photo.001.jpg");
            CreateFile("photo.002.jpg");

            var result = OutputPathResolver.GetUniquePath(input, ".jpg");

            Assert.AreEqual(Path.Combine(_dir, "photo.003.jpg"), result);
        }

        [TestMethod]
        public void PreservesDotsInBaseName()
        {
            var input = CreateFile("my.holiday.photo.webp");

            var result = OutputPathResolver.GetUniquePath(input, ".png");

            Assert.AreEqual(Path.Combine(_dir, "my.holiday.photo.png"), result);
        }

        [TestMethod]
        public void Throws_WhenAllSuffixesAreTaken()
        {
            var input = CreateFile("photo.png");
            CreateFile("photo.jpg");
            for (var i = 1; i <= 999; i++)
            {
                CreateFile($"photo.{i:000}.jpg");
            }

            Assert.ThrowsException<IOException>(() => OutputPathResolver.GetUniquePath(input, ".jpg"));
        }

        [TestMethod]
        public void WithSubfolder_WritesIntoSubfolder()
        {
            var input = CreateFile("photo.png");
            var settings = new AppSettings { UseSubfolder = true, SubfolderName = "_out" };
            var result = OutputPathResolver.GetUniquePath(input, ".jpg", settings);
            Assert.AreEqual(Path.Combine(_dir, "_out", "photo.jpg"), result);
        }

        [TestMethod]
        public void ApplyPattern_ReplacesNameAndExt()
        {
            var result = OutputPathResolver.ApplyPattern("{name}-copy.{ext}", "photo", ".jpg", 0, 0);
            Assert.AreEqual("photo-copy.jpg", result);
        }

        [TestMethod]
        public void ApplyPattern_ReplacesWidthAndHeight()
        {
            var result = OutputPathResolver.ApplyPattern("{name}.{width}x{height}", "img", ".png", 1920, 1080);
            Assert.AreEqual("img.1920x1080", result);
        }
    }
}
