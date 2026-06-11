using QConvert.Core;

using Xunit;

namespace QConvert.Tests
{
    public class ResizeMathTests
    {
        [Theory]
        [InlineData(4000, 3000, 1920, 1080, 1440, 1080)] // landscape limited by height
        [InlineData(3000, 4000, 1920, 1080, 810, 1080)]  // portrait limited by height
        [InlineData(4000, 1000, 1920, 1080, 1920, 480)]  // wide limited by width
        [InlineData(1920, 1080, 1920, 1080, 1920, 1080)] // exact match unchanged
        public void Fit_ScalesDownPreservingAspect(int srcW, int srcH, int boxW, int boxH, int expectedW, int expectedH)
        {
            var result = ResizeMath.Fit(new PixelSize(srcW, srcH), new PixelSize(boxW, boxH));

            Assert.Equal(new PixelSize(expectedW, expectedH), result);
        }

        [Fact]
        public void Fit_NeverUpscales()
        {
            var result = ResizeMath.Fit(new PixelSize(800, 600), new PixelSize(1920, 1080));

            Assert.Equal(new PixelSize(800, 600), result);
        }

        [Theory]
        [InlineData(4000, 3000, 1920, 1080)]
        [InlineData(1000, 700, 1920, 1080)]  // upscaling required
        [InlineData(4032, 3024, 1920, 1080)]
        [InlineData(500, 2000, 1920, 1080)]
        public void Cover_CropIsExactlyBoxSize(int srcW, int srcH, int boxW, int boxH)
        {
            var plan = ResizeMath.Cover(new PixelSize(srcW, srcH), new PixelSize(boxW, boxH));

            Assert.Equal(boxW, plan.Crop.Width);
            Assert.Equal(boxH, plan.Crop.Height);
            Assert.True(plan.Scaled.Width >= boxW);
            Assert.True(plan.Scaled.Height >= boxH);
            Assert.True(plan.Crop.X >= 0 && plan.Crop.X + boxW <= plan.Scaled.Width);
            Assert.True(plan.Crop.Y >= 0 && plan.Crop.Y + boxH <= plan.Scaled.Height);
        }

        [Fact]
        public void Cover_CropIsCentered()
        {
            // 4000x3000 covering 1920x1080 scales to 1920x1440; 360px trimmed top+bottom.
            var plan = ResizeMath.Cover(new PixelSize(4000, 3000), new PixelSize(1920, 1080));

            Assert.Equal(new PixelSize(1920, 1440), plan.Scaled);
            Assert.Equal(0, plan.Crop.X);
            Assert.Equal(180, plan.Crop.Y);
        }

        [Fact]
        public void AspectCrop_TrimsLeftAndRight_WhenTooWide()
        {
            // 2000x1000 to 4:3 → width becomes 1333, centered.
            var rect = ResizeMath.AspectCrop(new PixelSize(2000, 1000), 4, 3);

            Assert.Equal(1333, rect.Width);
            Assert.Equal(1000, rect.Height);
            Assert.Equal(333, rect.X);
            Assert.Equal(0, rect.Y);
        }

        [Fact]
        public void AspectCrop_TrimsTopAndBottom_WhenTooTall()
        {
            // 1000x2000 to 4:3 → height becomes 750, centered.
            var rect = ResizeMath.AspectCrop(new PixelSize(1000, 2000), 4, 3);

            Assert.Equal(1000, rect.Width);
            Assert.Equal(750, rect.Height);
            Assert.Equal(0, rect.X);
            Assert.Equal(625, rect.Y);
        }

        [Fact]
        public void AspectCrop_ReturnsFullImage_WhenRatioMatches()
        {
            var rect = ResizeMath.AspectCrop(new PixelSize(1600, 1200), 4, 3);

            Assert.Equal(new PixelRect(0, 0, 1600, 1200), rect);
        }

        [Fact]
        public void AspectCrop_HandlesEquivalentRatios()
        {
            // 8:6 is the same as 4:3.
            var rect = ResizeMath.AspectCrop(new PixelSize(1600, 1200), 8, 6);

            Assert.Equal(new PixelRect(0, 0, 1600, 1200), rect);
        }
    }
}
