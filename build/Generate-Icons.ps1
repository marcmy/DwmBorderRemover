#requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

if (-not ('DwmBorderRemoverIconGenerator' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DwmBorderRemoverIconGenerator
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };

    public static void Generate(string outputPath)
    {
        string directory = Path.GetDirectoryName(outputPath);
        if (!String.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var frames = new List<byte[]>();
        foreach (int size in Sizes)
            frames.Add(RenderPng(size));

        using (var stream = File.Create(outputPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0); // Reserved
            writer.Write((ushort)1); // Icon
            writer.Write((ushort)Sizes.Length);

            int imageOffset = 6 + (16 * Sizes.Length);
            for (int index = 0; index < Sizes.Length; index++)
            {
                int size = Sizes[index];
                byte[] frame = frames[index];

                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)0);  // Palette colors
                writer.Write((byte)0);  // Reserved
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)frame.Length);
                writer.Write((uint)imageOffset);
                imageOffset += frame.Length;
            }

            foreach (byte[] frame in frames)
                writer.Write(frame);
        }
    }

    private static byte[] RenderPng(int size)
    {
        using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            if (size <= 32)
                DrawCompactGlyph(graphics, size);
            else
                DrawApplicationGlyph(graphics, size);

            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                return memory.ToArray();
            }
        }
    }

    private static void DrawCompactGlyph(Graphics graphics, int size)
    {
        float margin = Math.Max(1.0f, size * 0.08f);
        float outline = Math.Max(1.0f, size * 0.075f);
        float radius = size * 0.25f;
        RectangleF outer = new RectangleF(margin, margin, size - (2 * margin), size - (2 * margin));
        RectangleF inner = new RectangleF(
            margin + outline,
            margin + outline,
            size - (2 * (margin + outline)),
            size - (2 * (margin + outline)));

        using (var outerBrush = new SolidBrush(Color.FromArgb(255, 91, 50, 190)))
        using (var innerBrush = new SolidBrush(Color.FromArgb(255, 132, 86, 245)))
        using (GraphicsPath outerPath = RoundedRectangle(outer, radius))
        using (GraphicsPath innerPath = RoundedRectangle(inner, Math.Max(1.0f, radius - outline)))
        {
            graphics.FillPath(outerBrush, outerPath);
            graphics.FillPath(innerBrush, innerPath);
        }

        PointF[] check =
        {
            new PointF(size * 0.27f, size * 0.53f),
            new PointF(size * 0.44f, size * 0.69f),
            new PointF(size * 0.74f, size * 0.35f)
        };

        float width = Math.Max(1.7f, size * 0.105f);
        using (var shadow = RoundedPen(Color.FromArgb(175, 24, 17, 43), width + Math.Max(1.0f, width * 0.38f)))
        using (var white = RoundedPen(Color.FromArgb(255, 250, 250, 253), width))
        {
            graphics.DrawLines(shadow, check);
            graphics.DrawLines(white, check);
        }
    }

    private static void DrawApplicationGlyph(Graphics graphics, int size)
    {
        float margin = size * 0.095f;
        float border = Math.Max(2.0f, size * 0.045f);
        float radius = size * 0.20f;
        float shadowOffset = Math.Max(1.0f, size * 0.024f);

        RectangleF shadowRect = new RectangleF(
            margin + shadowOffset,
            margin + shadowOffset,
            size - (2 * margin),
            size - (2 * margin));
        RectangleF outer = new RectangleF(margin, margin, size - (2 * margin), size - (2 * margin));
        RectangleF inner = new RectangleF(
            margin + border,
            margin + border,
            size - (2 * (margin + border)),
            size - (2 * (margin + border)));

        using (var shadowBrush = new SolidBrush(Color.FromArgb(105, 0, 0, 0)))
        using (var purpleBrush = new SolidBrush(Color.FromArgb(255, 132, 86, 245)))
        using (var panelBrush = new SolidBrush(Color.FromArgb(255, 35, 38, 49)))
        using (GraphicsPath shadowPath = RoundedRectangle(shadowRect, radius))
        using (GraphicsPath outerPath = RoundedRectangle(outer, radius))
        using (GraphicsPath innerPath = RoundedRectangle(inner, Math.Max(1.0f, radius - border)))
        {
            graphics.FillPath(shadowBrush, shadowPath);
            graphics.FillPath(purpleBrush, outerPath);
            graphics.FillPath(panelBrush, innerPath);
        }

        float titleY = size * 0.34f;
        using (var titlePen = new Pen(Color.FromArgb(255, 132, 86, 245), Math.Max(1.0f, border * 0.55f)))
            graphics.DrawLine(titlePen, inner.Left, titleY, inner.Right, titleY);

        if (size >= 64)
        {
            Color[] dots =
            {
                Color.FromArgb(255, 255, 112, 112),
                Color.FromArgb(255, 255, 201, 96),
                Color.FromArgb(255, 112, 220, 151)
            };

            float dotRadius = Math.Max(1.0f, size * 0.018f);
            float firstX = inner.Left + (size * 0.065f);
            float centerY = inner.Top + (size * 0.070f);
            for (int index = 0; index < dots.Length; index++)
            {
                float centerX = firstX + (index * size * 0.065f);
                using (var brush = new SolidBrush(dots[index]))
                    graphics.FillEllipse(brush, centerX - dotRadius, centerY - dotRadius, dotRadius * 2, dotRadius * 2);
            }
        }

        PointF[] check =
        {
            new PointF(size * 0.32f, size * 0.58f),
            new PointF(size * 0.45f, size * 0.70f),
            new PointF(size * 0.70f, size * 0.43f)
        };

        float checkWidth = Math.Max(2.0f, size * 0.075f);
        using (var shadow = RoundedPen(Color.FromArgb(150, 0, 0, 0), checkWidth + Math.Max(1.0f, checkWidth * 0.35f)))
        using (var white = RoundedPen(Color.FromArgb(255, 248, 249, 252), checkWidth))
        {
            graphics.DrawLines(shadow, check);
            graphics.DrawLines(white, check);
        }
    }

    private static Pen RoundedPen(Color color, float width)
    {
        return new Pen(color, width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
    }

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        float diameter = Math.Min(radius * 2.0f, Math.Min(rectangle.Width, rectangle.Height));
        var path = new GraphicsPath();

        if (diameter <= 1.0f)
        {
            path.AddRectangle(rectangle);
            return path;
        }

        RectangleF arc = new RectangleF(rectangle.X, rectangle.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
'@
}

[DwmBorderRemoverIconGenerator]::Generate([IO.Path]::GetFullPath($OutputPath))
