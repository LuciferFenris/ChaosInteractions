using System.Net;
using System.Text;
using System.Text.Json;

namespace ChaosInteractions;

public sealed class ChaosApiServer : IDisposable
{
    private readonly MainForm mainForm;
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Task listenTask;
    private bool disposed;

    public ChaosApiServer(MainForm mainForm)
    {
        this.mainForm = mainForm;
        listener.Prefixes.Add("http://127.0.0.1:28914/");
        listener.Start();
        listenTask = Task.Run(ListenAsync);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellationTokenSource.Cancel();

        try
        {
            listener.Stop();
            listener.Close();
        }
        catch
        {
            // Ignore shutdown exceptions.
        }

        try
        {
            listenTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore shutdown timeout and task cancellation noise.
        }

        cancellationTokenSource.Dispose();
    }

    private async Task ListenAsync()
    {
        try
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context));
            }
        }
        catch (HttpListenerException)
        {
            // Listener stopped.
        }
        catch (ObjectDisposedException)
        {
            // Listener stopped.
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath.Trim('/').ToLowerInvariant() ?? string.Empty;

            if (request.HttpMethod == "GET" && path == "status")
            {
                await WriteJsonAsync(context.Response, new
                {
                    inversionEnabled = mainForm.IsRemapperEnabled,
                    scrambleMode = mainForm.IsScrambleMode,
                    muteEnabled = mainForm.IsMuted,
                    blurEnabled = mainForm.IsBlurEnabled,
                    blurStrength = mainForm.BlurStrength,
                }).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod is not ("GET" or "POST"))
            {
                context.Response.StatusCode = 405;
                await WriteTextAsync(context.Response, "Method not allowed").ConfigureAwait(false);
                return;
            }

            switch (path)
            {
                case "toggle/invert":
                    mainForm.InvokeAppAction(() => mainForm.ToggleRemapperFromApi());
                    await WriteJsonAsync(context.Response, new { ok = true, action = "invert" }).ConfigureAwait(false);
                    return;
                case "toggle/mute":
                    mainForm.InvokeAppAction(() => mainForm.ToggleSystemMuteFromApi());
                    await WriteJsonAsync(context.Response, new { ok = true, action = "mute" }).ConfigureAwait(false);
                    return;
                case "toggle/blur":
                    mainForm.InvokeAppAction(mainForm.ToggleBlurFilterFromApi);
                    await WriteJsonAsync(context.Response, new { ok = true, action = "blur" }).ConfigureAwait(false);
                    return;
                case "mode/scramble":
                    var scramble = GetBoolQuery(request.Url, "enabled");
                    mainForm.InvokeAppAction(() => mainForm.SetScrambleModeFromApi(scramble));
                    await WriteJsonAsync(context.Response, new { ok = true, scramble }).ConfigureAwait(false);
                    return;
                case "blur/strength":
                    var strength = GetIntQuery(request.Url, "value", 70, 0, 100) / 100f;
                    mainForm.InvokeAppAction(() => mainForm.SetBlurStrengthFromApi(strength));
                    await WriteJsonAsync(context.Response, new { ok = true, blurStrength = strength }).ConfigureAwait(false);
                    return;
                default:
                    context.Response.StatusCode = 404;
                    await WriteTextAsync(context.Response, "Not found").ConfigureAwait(false);
                    return;
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context.Response, new { ok = false, error = ex.Message }).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static async Task WriteTextAsync(HttpListenerResponse response, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static bool GetBoolQuery(Uri? url, string name)
    {
        if (url is null)
        {
            return false;
        }

        var value = GetQueryValue(url, name);
        return value is not null && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static int GetIntQuery(Uri? url, string name, int defaultValue, int min, int max)
    {
        if (url is null)
        {
            return defaultValue;
        }

        var value = GetQueryValue(url, name);
        if (!int.TryParse(value, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static string? GetQueryValue(Uri url, string name)
    {
        var query = url.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(Uri.UnescapeDataString(parts[0]), name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }
}