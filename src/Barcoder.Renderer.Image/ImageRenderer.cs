using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Barcoder.Renderers;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using ImageSharp = SixLabors.ImageSharp;

namespace Barcoder.Renderer.Image
{
    public sealed class ImageRenderer : IRenderer
    {
        private readonly IImageEncoder _imageEncoder;
        private readonly int _pixelSize;
        private readonly int _barHeightFor1DBarcode;
        private readonly bool _includeEanContentAsText;
        private readonly string _eanFontFamily;

        public ImageRenderer(
            int pixelSize = 10,
            int barHeightFor1DBarcode = 40,
            ImageFormat imageFormat = ImageFormat.Png,
            int jpegQuality = 75,
            bool includeEanContentAsText = false,
            string eanFontFamily = null)
        {
            if (pixelSize <= 0) throw new ArgumentOutOfRangeException(nameof(pixelSize), "Value must be larger than zero");
            if (barHeightFor1DBarcode <= 0) throw new ArgumentOutOfRangeException(nameof(barHeightFor1DBarcode), "Value must be larger than zero");
            if (jpegQuality < 0 || jpegQuality > 100) throw new ArgumentOutOfRangeException(nameof(jpegQuality), "Value must be a value between 0 and 100");
            
            _pixelSize = pixelSize;
            _barHeightFor1DBarcode = barHeightFor1DBarcode;
            _imageEncoder = GetImageEncoder(imageFormat, jpegQuality);
            _includeEanContentAsText = includeEanContentAsText;
            _eanFontFamily = eanFontFamily ?? "Arial";
        }

        private static IImageEncoder GetImageEncoder(ImageFormat imageFormat, int jpegQuality)
        {
            switch (imageFormat)
            {
            case ImageFormat.Bmp:
                return new BmpEncoder();
            case ImageFormat.Gif:
                return new GifEncoder();
            case ImageFormat.Jpeg:
                return new JpegEncoder { Quality = jpegQuality };
            case ImageFormat.Png:
                return new PngEncoder();
            default:
                throw new NotSupportedException($"Requested image format {imageFormat} is not supported");
            }
        }

        public void Render(IBarcode barcode, Stream outputStream)
        {
            barcode = barcode ?? throw new ArgumentNullException(nameof(barcode));
            outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
            if (barcode.Bounds.Y == 1)
                Render1D(barcode, outputStream);
            else if (barcode.Bounds.Y > 1)
                Render2D(barcode, outputStream);
            else
                throw new NotSupportedException($"Y value of {barcode.Bounds.Y} is invalid");
        }

        private void Render1D(IBarcode barcode, Stream outputStream)
        {
            int width = (barcode.Bounds.X + 2 * barcode.Margin) * _pixelSize;
            int height = (_barHeightFor1DBarcode + 2 * barcode.Margin) * _pixelSize;
            int contentMargin = 10;
            using (var image = new ImageSharp.Image<Gray8>(width, height))
            {
                image.Mutate(ctx =>
                {
                    var black = new Gray8(0);
                    ctx.Fill(new Gray8(255));
                    for (var x = 0; x < barcode.Bounds.X; x++)
                    {
                        if (!barcode.At(x, 0))
                            continue;
                        ctx.FillPolygon(
                            black,
                            new Vector2((barcode.Margin + x) * _pixelSize, barcode.Margin * _pixelSize),
                            new Vector2((barcode.Margin + x + 1) * _pixelSize, barcode.Margin * _pixelSize),
                            new Vector2((barcode.Margin + x + 1) * _pixelSize, (_barHeightFor1DBarcode + barcode.Margin) * _pixelSize),
                            new Vector2((barcode.Margin + x) * _pixelSize, (_barHeightFor1DBarcode + barcode.Margin) * _pixelSize));
                    }
                });

                if (_includeEanContentAsText && (barcode.Metadata.CodeKind == BarcodeType.EAN13 || barcode.Metadata.CodeKind == BarcodeType.EAN8))
                {
                    if (barcode.Metadata.CodeKind == BarcodeType.EAN13)
                    {
                        //    var longerBarsEan13 = new int[] { 0, 2, 46, 48, 92, 94 };
                        RenderWhiteRect(image, barcode.Margin + 3, _barHeightFor1DBarcode, 43, contentMargin);
                        RenderWhiteRect(image, barcode.Margin + 49, _barHeightFor1DBarcode, 43, contentMargin);
                    }
                    else
                    {
                        //    var longerBarsEan8 = new int[] { 0, 2, 32, 34, 64, 66 };
                        RenderWhiteRect(image, barcode.Margin + 3, _barHeightFor1DBarcode, 29, contentMargin);
                        RenderWhiteRect(image, barcode.Margin + 35, _barHeightFor1DBarcode, 29, contentMargin);
                    }
                    
                    RenderText(barcode, image, contentMargin);
                }

                image.Save(outputStream, _imageEncoder);
            }
        }
        private void RenderWhiteRect(Image<Gray8> image, int x, int y, int width, int height)
        {
            var white = new Gray8(255);
            image.Mutate(ctx => {
                ctx.FillPolygon(
                            white,
                            new Vector2(x * _pixelSize, y * _pixelSize),
                            new Vector2((x + width) * _pixelSize, y * _pixelSize),
                            new Vector2((x + width) * _pixelSize, (y + height) * _pixelSize),
                            new Vector2(x * _pixelSize, (y + height) * _pixelSize));
            });
        }
        private void RenderText(IBarcode barcode, Image<Gray8> image, int contentMargin)
        {
            image.Mutate(ctx =>
            {
                var black = new Gray8(0);
                Font font = GetFont();
                float y = (_barHeightFor1DBarcode + (contentMargin / 4)) * _pixelSize;
                if (barcode.Metadata.CodeKind == BarcodeType.EAN8)
                {
                    string text1 = barcode.Content.Substring(0, 4);
                    string text2 = barcode.Content.Substring(4);
                    image.Mutate(x => x.DrawText(text1, font, black, new PointF(17.5f * (float)_pixelSize, y)));
                    image.Mutate(x => x.DrawText(text2, font, black, new PointF(49.5f * (float)_pixelSize, y)));
                }
                else if (barcode.Metadata.CodeKind == BarcodeType.EAN13)
                {
                    string text1 = barcode.Content.Substring(0, 1);
                    string text2 = barcode.Content.Substring(1, 6);
                    string text3 = barcode.Content.Substring(7);
                    image.Mutate(x => x.DrawText(text1, font, black, new PointF(4 * _pixelSize, y)));
                    image.Mutate(x => x.DrawText(text2, font, black, new PointF(20 * _pixelSize, y)));
                    image.Mutate(x => x.DrawText(text3, font, black, new PointF(65 * _pixelSize, y)));
                }
            });
        }
        private void Render2D(IBarcode barcode, Stream outputStream)
        {
            int width = (barcode.Bounds.X + 2 * barcode.Margin) * _pixelSize;
            int height = (barcode.Bounds.Y + 2 * barcode.Margin) * _pixelSize;

            using (var image = new ImageSharp.Image<Gray8>(width, height))
            {
                image.Mutate(ctx =>
                {
                    var black = new Gray8(0);
                    ctx.Fill(new Gray8(255));
                    for (var y = 0; y < barcode.Bounds.Y; y++)
                    {
                        for (var x = 0; x < barcode.Bounds.X; x++)
                        {
                            if (!barcode.At(x, y))
                                continue;
                            ctx.FillPolygon(
                                black,
                                new Vector2((barcode.Margin + x) * _pixelSize, (barcode.Margin + y) * _pixelSize),
                                new Vector2((barcode.Margin + x + 1) * _pixelSize, (barcode.Margin + y) * _pixelSize),
                                new Vector2((barcode.Margin + x + 1) * _pixelSize, (barcode.Margin + y + 1) * _pixelSize),
                                new Vector2((barcode.Margin + x) * _pixelSize, (barcode.Margin + y + 1) * _pixelSize));
                        }
                    }
                });

                image.Save(outputStream, _imageEncoder);
            }
        }

        private Font GetFont()
        {
            float fontSize = 9;
            return SystemFonts.CreateFont(_eanFontFamily, fontSize * _pixelSize, FontStyle.Regular);
        }
    }
}
