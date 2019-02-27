using System.IO;
using Barcoder.Code128;
using FluentAssertions;
using Xunit;

namespace Barcoder.Renderer.Svg.Tests
{
    public class SvgRendererTests
    {
        [Fact]
        public void Render_Barcode1D()
        {
            // Arrange
            IBarcode barcode = Code128Encoder.Encode("Wikipedia");
            var renderer = new SvgRenderer();

            // Act
            string svg;
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream))
            {
                renderer.Render(barcode, stream);
                stream.Position = 0;
                svg = reader.ReadToEnd();
            }

            // Assert
            svg.Length.Should().BeGreaterOrEqualTo(0);

            string expected;
            using (Stream stream = typeof(SvgRendererTests).Assembly.GetManifestResourceStream("Barcoder.Renderer.Svg.Tests.Barcode1D.ExpectedSvgOutput.txt"))
            using (var reader = new StreamReader(stream))
                expected = reader.ReadToEnd().Replace("\r", "").Replace("\n", "");

            var actual = svg.Replace("\r", "").Replace("\n", "");
            actual.Should().Be(expected);
        }
    }
}
