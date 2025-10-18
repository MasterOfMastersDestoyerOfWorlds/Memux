using System.Net.Http;

namespace Memux.App;

public static class ModelManager
{
    public static async Task<(string? depthModel, string? objectModel, string? objectClasses, string? tessData)> EnsureAsync(
        bool useGpu,
        Action<Memux.UI.ModelProgress> report,
        CancellationToken cancellationToken = default)
    {
        string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Memux", "models");
        Directory.CreateDirectory(baseDir);

        // Model file names (defaults). You can override via env vars.
        string depthFile = Environment.GetEnvironmentVariable("MEMUX_DEPTH_FILE") ?? "midas.onnx";
        string objectFile = Environment.GetEnvironmentVariable("MEMUX_OBJECT_FILE") ?? "yolov8.onnx";
        string classesFile = Environment.GetEnvironmentVariable("MEMUX_CLASSES_FILE") ?? "coco.names";
        string tessDirName = Environment.GetEnvironmentVariable("MEMUX_TESS_DIR") ?? "tessdata";

        string depthPath = Path.Combine(baseDir, depthFile);
        string objectPath = Path.Combine(baseDir, objectFile);
        string classesPath = Path.Combine(baseDir, classesFile);
        string tessPath = Path.Combine(baseDir, tessDirName);

        var progress = new Memux.UI.ModelProgress
        {
            DepthStatus = File.Exists(depthPath) ? "found" : "pending",
            ObjectsStatus = File.Exists(objectPath) && File.Exists(classesPath) ? "found" : "pending",
            OcrStatus = Directory.Exists(tessPath) ? "found" : "pending"
        };
        report(progress);

        var tasks = new List<Task>();
        var client = new HttpClient();
        string baseUrl = Environment.GetEnvironmentVariable("MEMUX_MODEL_BASE_URL") ?? string.Empty; // e.g., https://your-cdn/models/

        if (!File.Exists(depthPath) && !string.IsNullOrEmpty(baseUrl))
        {
            tasks.Add(DownloadFileAsync(client, new Uri(new Uri(baseUrl), depthFile), depthPath, p => { progress.DepthPercent = p; progress.DepthStatus = $"downloading {p}%"; report(progress); }, cancellationToken));
        }

        if ((!File.Exists(objectPath) || !File.Exists(classesPath)) && !string.IsNullOrEmpty(baseUrl))
        {
            tasks.Add(DownloadFileAsync(client, new Uri(new Uri(baseUrl), objectFile), objectPath, p => { progress.ObjectsPercent = p; progress.ObjectsStatus = $"weights {p}%"; report(progress); }, cancellationToken));
            tasks.Add(DownloadFileAsync(client, new Uri(new Uri(baseUrl), classesFile), classesPath, p => { progress.ObjectsPercent = Math.Min(99, p); progress.ObjectsStatus = $"labels {p}%"; report(progress); }, cancellationToken));
        }

        if (!Directory.Exists(tessPath) && !string.IsNullOrEmpty(baseUrl))
        {
            Directory.CreateDirectory(tessPath);
            // Minimal eng.traineddata as example
            string tessEng = "eng.traineddata";
            tasks.Add(DownloadFileAsync(client, new Uri(new Uri(baseUrl), Path.Combine("tessdata", tessEng)), Path.Combine(tessPath, tessEng), p => { progress.OcrPercent = p; progress.OcrStatus = $"eng {p}%"; report(progress); }, cancellationToken));
        }

        try
        {
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }
        catch
        {
            // Ignore download errors; user can provide models manually later
        }

        // Final statuses
        progress.DepthPercent = File.Exists(depthPath) ? 100 : progress.DepthPercent;
        progress.ObjectsPercent = (File.Exists(objectPath) && File.Exists(classesPath)) ? 100 : progress.ObjectsPercent;
        progress.OcrPercent = Directory.Exists(tessPath) ? 100 : progress.OcrPercent;
        progress.DepthStatus = File.Exists(depthPath) ? "ready" : progress.DepthStatus;
        progress.ObjectsStatus = (File.Exists(objectPath) && File.Exists(classesPath)) ? "ready" : progress.ObjectsStatus;
        progress.OcrStatus = Directory.Exists(tessPath) ? "ready" : progress.OcrStatus;
        report(progress);

        // Return whatever is available
        return (
            File.Exists(depthPath) ? depthPath : null,
            File.Exists(objectPath) ? objectPath : null,
            File.Exists(classesPath) ? classesPath : null,
            Directory.Exists(tessPath) ? tessPath : null
        );
    }

    private static async Task DownloadFileAsync(HttpClient client, Uri uri, string destinationPath, Action<int> onProgress, CancellationToken ct)
    {
        using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long read = 0;
        int lastPercent = 0;
        while (true)
        {
            int n = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            if (n == 0) break;
            await fs.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0)
            {
                int percent = (int)(read * 100 / total);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    onProgress(percent);
                }
            }
        }
        onProgress(100);
    }
}




