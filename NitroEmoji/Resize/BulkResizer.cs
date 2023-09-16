using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NitroEmoji.Resize;

internal class BulkResizer
{
    private static Task<int> RunProcessAsync(Process process)
    {
        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

        process.EnableRaisingEvents = true;

        process.Exited += (sender, args) =>
        {
            tcs.SetResult(process.ExitCode);
            process.Dispose();
        };

        process.Start();

        return tcs.Task;
    }

    public static async Task ResizeGif(string path)
    {
        Process gifsicle = new Process
        {
            StartInfo =
            {
                FileName = "gifsicle.exe",
                Arguments = "--batch --resize-fit 50x50 \"" + path + "\"",
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };
        await RunProcessAsync(gifsicle);
    }

    public static async Task ResizeGifs(string dir)
    {
        await ResizeGif(dir + "\\*.gif");
    }

    public static async Task ResizePng(string path)
    {
        BitmapImage b = new BitmapImage(new Uri(path));
        int max = Math.Max(b.PixelWidth, b.PixelHeight);
        if (max <= 50)
        {
            return;
        }

        double factor = 50.0 / max;
        TransformedBitmap t = new TransformedBitmap(b, new ScaleTransform(factor, factor));
        BitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(t));

        using (FileStream fileStream = new FileStream(path, FileMode.Create))
        {
            encoder.Save(fileStream);
        }
    }

    public static async Task ResizePngs(string dir)
    {
        DirectoryInfo d = new DirectoryInfo(dir);
        foreach (FileInfo file in d.GetFiles("*.png"))
        {
            await ResizePng(file.FullName);
        }
    }
}