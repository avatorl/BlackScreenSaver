using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

// Generate a proper multi-size ICO file with 16x16, 32x32, 48x48, 256x256
// The icon is a black square with a dark gray border (matching GenerateIcon style)
int[] sizes = { 16, 32, 48, 256 };

// Build each size as a PNG-encoded image
var pngDataList = new List<byte[]>();
foreach (int size in sizes)
{
    using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.Clear(Color.Black);
        
        // Draw a dark gray border (1px for small, proportional for larger)
        int borderWidth = Math.Max(1, size / 16);
        using var pen = new Pen(Color.DarkGray, borderWidth);
        float half = borderWidth / 2f;
        g.DrawRectangle(pen, half, half, size - borderWidth, size - borderWidth);
    }
    using var ms = new MemoryStream();
    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
    pngDataList.Add(ms.ToArray());
}

// Write ICO file
// ICO format: Header (6 bytes) + Directory entries (16 bytes each) + Image data
using var fs = new FileStream(@"D:\GITHUB\blackscreensaver\Resources\icon.ico", FileMode.Create);
using var bw = new BinaryWriter(fs);

// Header
bw.Write((short)0);       // Reserved
bw.Write((short)1);       // Type: ICO
bw.Write((short)sizes.Length); // Number of images

// Calculate data offset: header(6) + entries(16 * count)
int dataOffset = 6 + 16 * sizes.Length;

// Write directory entries
for (int i = 0; i < sizes.Length; i++)
{
    byte w = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
    byte h = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
    bw.Write(w);                           // Width
    bw.Write(h);                           // Height
    bw.Write((byte)0);                     // Color palette
    bw.Write((byte)0);                     // Reserved
    bw.Write((short)1);                    // Color planes
    bw.Write((short)32);                   // Bits per pixel
    bw.Write(pngDataList[i].Length);       // Image data size
    bw.Write(dataOffset);                  // Image data offset
    dataOffset += pngDataList[i].Length;
}

// Write image data
for (int i = 0; i < sizes.Length; i++)
{
    bw.Write(pngDataList[i]);
}

Console.WriteLine("Multi-size icon created successfully!");
Console.WriteLine($"Total file size: {fs.Length} bytes");
Console.WriteLine($"Contains {sizes.Length} images: {string.Join(", ", sizes.Select(s => $"{s}x{s}"))}");
