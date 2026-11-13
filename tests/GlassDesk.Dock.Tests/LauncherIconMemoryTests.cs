using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using GlassDesk.Core.Imaging;

namespace GlassDesk.Dock.Tests;

public sealed class LauncherIconMemoryTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public LauncherIconMemoryTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ClearingSleepingLoaderReleasesQueuedPayloadsWithoutDecoding()
    {
        using var loader = (IDisposable)Activator.CreateInstance(
            LoaderType, nonPublic: true)!;
        LoaderType.GetMethod("SetSuspended", BindingFlags.Instance
            | BindingFlags.NonPublic)!.Invoke(loader, [true]);
        var payloads = new List<WeakReference>();
        var tasks = new List<Task<BitmapImage?>>();
        // 用真实排队闭包检查引用能否释放，不依赖工作集修剪或强制清空系统内存。
        for (var index = 0; index < 96; index++)
            QueuePayload(loader, index, payloads, tasks);
        LoaderType.GetMethod("Clear", BindingFlags.Instance
            | BindingFlags.NonPublic)!.Invoke(loader, null);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var retained = payloads.Count(reference => reference.IsAlive);
        _output.WriteLine($"休眠清理后保留的 256 KiB 请求负载：{retained}/96");
        _output.WriteLine($"已完成请求：{tasks.Count(task => task.IsCompleted)}/96");
        Assert.InRange(retained, 0, 1);
        Assert.All(tasks, task => Assert.True(task.IsCompletedSuccessfully));
        Assert.All(await Task.WhenAll(tasks), image => Assert.Null(image));
        LoaderType.GetMethod("SetSuspended", BindingFlags.Instance
            | BindingFlags.NonPublic)!.Invoke(loader, [false]);
        Assert.NotNull(await Load(loader, "wake", () => Image(32, 32))
            .WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void QueuePayload(object loader, int index,
        List<WeakReference> payloads, List<Task<BitmapImage?>> tasks)
    {
        var payload = new byte[256 * 1024];
        payloads.Add(new WeakReference(payload));
        tasks.Add(Load(loader, $"pending-{index}", () =>
        {
            GC.KeepAlive(payload);
            throw new InvalidOperationException("已清理请求不应继续解码。");
        }));
    }

    private static Type LoaderType => Assembly.Load("GlassDesk.Launcher").GetType("GlassDesk.Launcher.LauncherIconLoader", true)!;
    private static Task<BitmapImage?> Load(object loader, string key, Func<BitmapImage?> resolve) =>
        (Task<BitmapImage?>)LoaderType.GetMethod("LoadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(loader, [key, resolve])!;

    [Fact]
    public void ByteBudgetEvictsLeastRecentlyUsedDecodedImage()
    {
        StaTest.Run(() =>
        {
            using var loader = (IDisposable)Activator.CreateInstance(LoaderType,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [8192L], null)!;
            var calls = new Dictionary<string, int>();
            BitmapImage? Resolve(string key)
            {
                calls[key] = calls.GetValueOrDefault(key) + 1;
                return Image(32, 32);
            }
            foreach (var key in new[] { "a", "b", "b", "c", "b" })
                Assert.NotNull(Load(loader, key, () => Resolve(key)).GetAwaiter().GetResult());
            Assert.Equal(1, calls["b"]);
            Assert.NotNull(Load(loader, "a", () => Resolve("a")).GetAwaiter().GetResult());
            Assert.Equal(2, calls["a"]);
        });
    }

    [Fact]
    public void SleepingDefersQueuedDecodeAndResumesWithoutLosingRequest()
    {
        StaTest.Run(() =>
        {
            using var loader = (IDisposable)Activator.CreateInstance(LoaderType, nonPublic: true)!;
            var suspend = LoaderType.GetMethod("SetSuspended", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(suspend);
            suspend.Invoke(loader, [true]);
            using var entered = new ManualResetEventSlim();
            var task = Load(loader, "queued", () => { entered.Set(); return Image(32, 32); });
            Assert.False(entered.Wait(TimeSpan.FromMilliseconds(150)));
            Assert.False(task.IsCompleted);
            suspend.Invoke(loader, [false]);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(3)));
            Assert.NotNull(task.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult());
        });
    }

    [Fact]
    public void DisposingSleepingLoaderCompletesPendingRequest()
    {
        StaTest.Run(() =>
        {
            using var loader = (IDisposable)Activator.CreateInstance(LoaderType, nonPublic: true)!;
            var suspend = LoaderType.GetMethod("SetSuspended", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(suspend);
            suspend.Invoke(loader, [true]);
            var task = Load(loader, "queued", () => throw new InvalidOperationException("decode must remain asleep"));
            loader.Dispose();
            Assert.Null(task.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult());
        });
    }

    [Fact]
    public void CustomImageDecodesAtPhysicalDisplaySizeAndPreservesAspectRatio()
    {
        StaTest.Run(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), "GlassDesk-icon-decode-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, PngWriter.EncodeBgra(new byte[1024 * 512 * 4], 1024, 512));
            try
            {
                var decoder = Assembly.Load("GlassDesk.Launcher").GetType("GlassDesk.Launcher.LauncherIconDecoder", true)!;
                var image = (BitmapImage)decoder.GetMethod("DecodeFile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [path, 120])!;
                Assert.Equal(120, image.PixelWidth);
                Assert.Equal(60, image.PixelHeight);
                Assert.True(image.IsFrozen);
            }
            finally { File.Delete(path); }
        });
    }

    private static BitmapImage Image(int width, int height)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(PngWriter.EncodeBgra(new byte[width * height * 4], width, height));
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit(); image.Freeze();
        return image;
    }
}
