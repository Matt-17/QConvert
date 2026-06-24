using Microsoft.VisualStudio.TestTools.UnitTesting;

using QConvert.Core;

namespace QConvert.Tests
{
    [TestClass]
    public class ResizeMathTests
    {
        [TestMethod] public void Fit_Landscape_LimitedByHeight() { var r = ResizeMath.Fit(new PixelSize(4000, 3000), new PixelSize(1920, 1080)); Assert.AreEqual(new PixelSize(1440, 1080), r); }
        [TestMethod] public void Fit_Portrait_LimitedByHeight()  { var r = ResizeMath.Fit(new PixelSize(3000, 4000), new PixelSize(1920, 1080)); Assert.AreEqual(new PixelSize(810, 1080), r); }
        [TestMethod] public void Fit_Wide_LimitedByWidth()        { var r = ResizeMath.Fit(new PixelSize(4000, 1000), new PixelSize(1920, 1080)); Assert.AreEqual(new PixelSize(1920, 480), r); }
        [TestMethod] public void Fit_ExactMatch_Unchanged()       { var r = ResizeMath.Fit(new PixelSize(1920, 1080), new PixelSize(1920, 1080)); Assert.AreEqual(new PixelSize(1920, 1080), r); }

        [TestMethod]
        public void Fit_NeverUpscales()
        {
            var result = ResizeMath.Fit(new PixelSize(800, 600), new PixelSize(1920, 1080));
            Assert.AreEqual(new PixelSize(800, 600), result);
        }

        [TestMethod]
        public void Cover_CropIsExactlyBoxSize_Downscale()
        {
            var plan = ResizeMath.Cover(new PixelSize(4000, 3000), new PixelSize(1920, 1080));
            Assert.AreEqual(1920, plan.Crop.Width); Assert.AreEqual(1080, plan.Crop.Height);
            Assert.IsTrue(plan.Scaled.Width >= 1920); Assert.IsTrue(plan.Scaled.Height >= 1080);
        }

        [TestMethod]
        public void Cover_CropIsExactlyBoxSize_Upscale()
        {
            var plan = ResizeMath.Cover(new PixelSize(1000, 700), new PixelSize(1920, 1080));
            Assert.AreEqual(1920, plan.Crop.Width); Assert.AreEqual(1080, plan.Crop.Height);
        }

        [TestMethod]
        public void Cover_CropIsCentered()
        {
            var plan = ResizeMath.Cover(new PixelSize(4000, 3000), new PixelSize(1920, 1080));
            Assert.AreEqual(new PixelSize(1920, 1440), plan.Scaled);
            Assert.AreEqual(0, plan.Crop.X);
            Assert.AreEqual(180, plan.Crop.Y);
        }

        [TestMethod]
        public void Cover_AnchorTop_CropsFromTop()
        {
            var plan = ResizeMath.Cover(new PixelSize(4000, 3000), new PixelSize(1920, 1080), CropAnchor.Top);
            Assert.AreEqual(0, plan.Crop.Y);
        }

        [TestMethod]
        public void Cover_AnchorBottom_CropsFromBottom()
        {
            var plan = ResizeMath.Cover(new PixelSize(4000, 3000), new PixelSize(1920, 1080), CropAnchor.Bottom);
            Assert.AreEqual(plan.Scaled.Height - 1080, plan.Crop.Y);
        }

        [TestMethod]
        public void Cover_Position_CropsAtNormalizedOffset()
        {
            var top = ResizeMath.Cover(new PixelSize(4000, 3000), new PixelSize(1920, 1080), 0.5, 0);
            var bottom = ResizeMath.Cover(new PixelSize(4000, 3000), new PixelSize(1920, 1080), 0.5, 1);

            Assert.AreEqual(0, top.Crop.Y);
            Assert.AreEqual(bottom.Scaled.Height - 1080, bottom.Crop.Y);
        }

        [TestMethod]
        public void AspectCrop_TrimsLeftAndRight_WhenTooWide()
        {
            var rect = ResizeMath.AspectCrop(new PixelSize(2000, 1000), 4, 3);
            Assert.AreEqual(1333, rect.Width);
            Assert.AreEqual(1000, rect.Height);
            Assert.AreEqual(333, rect.X);
            Assert.AreEqual(0, rect.Y);
        }

        [TestMethod]
        public void AspectCrop_TrimsTopAndBottom_WhenTooTall()
        {
            var rect = ResizeMath.AspectCrop(new PixelSize(1000, 2000), 4, 3);
            Assert.AreEqual(1000, rect.Width);
            Assert.AreEqual(750, rect.Height);
            Assert.AreEqual(0, rect.X);
            Assert.AreEqual(625, rect.Y);
        }

        [TestMethod]
        public void AspectCrop_Position_CropsAtNormalizedOffset()
        {
            var left = ResizeMath.AspectCrop(new PixelSize(2000, 1000), 4, 3, 0, 0.5);
            var right = ResizeMath.AspectCrop(new PixelSize(2000, 1000), 4, 3, 1, 0.5);

            Assert.AreEqual(0, left.X);
            Assert.AreEqual(2000 - right.Width, right.X);
        }

        [TestMethod]
        public void AspectCrop_ReturnsFullImage_WhenRatioMatches()
        {
            var rect = ResizeMath.AspectCrop(new PixelSize(1600, 1200), 4, 3);
            Assert.AreEqual(new PixelRect(0, 0, 1600, 1200), rect);
        }

        [TestMethod]
        public void AspectCrop_HandlesEquivalentRatios()
        {
            var rect = ResizeMath.AspectCrop(new PixelSize(1600, 1200), 8, 6);
            Assert.AreEqual(new PixelRect(0, 0, 1600, 1200), rect);
        }
    }
}
