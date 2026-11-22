using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Godot;
using Anomaly.Client.Shared.Speech;

namespace Anomaly.Client.Speech;

/// <summary>
/// D307: the speech models in the user folder (user://speech/&lt;model&gt;/). A model is downloaded only when the player turns its setting on;
/// each file goes to a .part file, is checked against its SHA-256 and only then takes its name, so a broken download never loads.
/// </summary>
public static class SpeechStore
{
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    public static string FolderOf(SpeechModel model) => ProjectSettings.GlobalizePath("user://speech/" + model.Folder);

    public static string PathOf(SpeechModel model, string file) => Path.Combine(FolderOf(model), file);

    /// <summary>Every file is there at its size (the hash was checked when it was downloaded).</summary>
    public static bool IsInstalled(SpeechModel model) =>
        model.Files.All(f => new FileInfo(PathOf(model, f.Name)) is { Exists: true } info && info.Length == f.Bytes);

    /// <summary>Downloads the missing files; <paramref name="progress"/> gets the fraction done (0 to 1) from a worker thread.</summary>
    public static async Task Download(SpeechModel model, Action<float> progress, CancellationToken cancel)
    {
        Directory.CreateDirectory(FolderOf(model));
        long done = 0;
        foreach (SpeechModelFile file in model.Files)
        {
            string path = PathOf(model, file.Name);
            if (new FileInfo(path) is { Exists: true } existing && existing.Length == file.Bytes)
            {
                done += file.Bytes;
                continue;
            }
            string part = path + ".part";
            using (HttpResponseMessage response = await Http.GetAsync(model.BaseUrl + file.From + file.Name, HttpCompletionOption.ResponseHeadersRead, cancel))
            {
                response.EnsureSuccessStatusCode();
                await using Stream body = await response.Content.ReadAsStreamAsync(cancel);
                await using FileStream output = File.Create(part);
                var buffer = new byte[1 << 16];
                long fileDone = 0;
                int read;
                while ((read = await body.ReadAsync(buffer, cancel)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancel);
                    fileDone += read;
                    if (fileDone > file.Bytes)
                        throw new InvalidDataException($"{file.Name} is larger than expected");
                    progress?.Invoke((float)((done + fileDone) / (double)model.Bytes));
                }
            }
            string hash;
            await using (FileStream check = File.OpenRead(part))
                hash = file.Sha256.Length == 40 ? await GitBlobSha1(check, file.Bytes, cancel) : Convert.ToHexStringLower(await SHA256.HashDataAsync(check, cancel));
            if (hash != file.Sha256)
            {
                File.Delete(part);
                throw new InvalidDataException($"{file.Name}: wrong hash");
            }
            File.Move(part, path, overwrite: true);
            done += file.Bytes;
        }
        progress?.Invoke(1f);
    }

    // D319: git's object id of a file: the SHA-1 of "blob <size>\0" and the bytes
    private static async Task<string> GitBlobSha1(Stream data, long bytes, CancellationToken cancel)
    {
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha1.AppendData(System.Text.Encoding.ASCII.GetBytes($"blob {bytes}\0"));
        var buffer = new byte[1 << 16];
        int read;
        while ((read = await data.ReadAsync(buffer, cancel)) > 0)
            sha1.AppendData(buffer, 0, read);
        return Convert.ToHexStringLower(sha1.GetHashAndReset());
    }
}

/// <summary>D307: the downloads the Settings window starts and shows; one at a time per model, kept across scenes.</summary>
public static class SpeechDownloads
{
    private static readonly Dictionary<SpeechModel, (float Progress, string Error, bool Running)> States = new();
    private static readonly object Lock = new();

    /// <summary>Starts the model's download unless it is installed or already downloading.</summary>
    public static void Ensure(SpeechModel model)
    {
        lock (Lock)
        {
            if (States.TryGetValue(model, out var state) && state.Running || SpeechStore.IsInstalled(model))
                return;
            States[model] = (0f, null, true);
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await SpeechStore.Download(model, p => { lock (Lock) States[model] = (p, null, true); }, CancellationToken.None);
                lock (Lock) States[model] = (1f, null, false);
            }
            catch (Exception e)
            {
                GD.PushWarning($"[anomaly] speech: {model.Title} download failed: {e.Message}");
                lock (Lock) States[model] = (0f, e.Message, false);
            }
        });
    }

    /// <summary>D310: there are files of the model on disk and no download is writing them: Settings may delete them.</summary>
    public static bool CanDelete(SpeechModel model)
    {
        lock (Lock)
            if (States.TryGetValue(model, out var state) && state.Running)
                return false;
        return Directory.Exists(SpeechStore.FolderOf(model));
    }

    /// <summary>D310: deletes the model's files (the engine using them must be disposed first); false when a download is running.</summary>
    public static bool Delete(SpeechModel model)
    {
        lock (Lock)
        {
            if (States.TryGetValue(model, out var state) && state.Running)
                return false;
            States.Remove(model);
        }
        try
        {
            if (Directory.Exists(SpeechStore.FolderOf(model)))
                Directory.Delete(SpeechStore.FolderOf(model), recursive: true);
            return true;
        }
        catch (IOException e)
        {
            GD.PushWarning($"[anomaly] speech: {model.Title} not deleted: {e.Message}");
            return false;
        }
        catch (UnauthorizedAccessException e)
        {
            GD.PushWarning($"[anomaly] speech: {model.Title} not deleted: {e.Message}");
            return false;
        }
    }

    /// <summary>A line for the Settings window: ready, downloading with its per cent, failed with why, or not downloaded with its size.</summary>
    public static string Status(SpeechModel model)
    {
        (float Progress, string Error, bool Running) state;
        lock (Lock)
            States.TryGetValue(model, out state);
        if (state.Running)
            return $"Downloading {model.Title}: {state.Progress * 100f:0}% of {model.Bytes / 1048576} MB";
        if (SpeechStore.IsInstalled(model))
            return $"{model.Title}: ready";
        if (state.Error != null)
            return $"{model.Title}: download failed ({state.Error}); turn the option off and on to retry";
        return $"{model.Title}: not downloaded ({model.Bytes / 1048576} MB, downloaded when turned on)";
    }
}
