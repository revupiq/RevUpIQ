using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace RevUpIQ.Admin.Infrastructure;

// Fetches the images named in a Photo Guess import file: Google Drive links
// (file/d/<id>/…, open?id=<id>, uc?id=<id>) or direct links to an image. Only
// public internet addresses are contacted, so a link in a sheet can't reach the
// server's own network.
public static class ImportImageClient
{
    // The formats the app can show, by content type, with the extension each is stored under
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    private static readonly Regex DriveLink = new(
        @"^https?://(?:drive|docs)\.google\.com/(?:file/(?:u/\d+/)?d/(?<id>[A-Za-z0-9_-]+)|(?:open|uc)\?(?:.*&)?id=(?<id>[A-Za-z0-9_-]+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string DriveNotShared =
        "Google Drive didn't share that image. In Drive choose Share → General access → " +
        "\"Anyone with the link\" (Viewer), then preview again.";

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        ConnectCallback = ConnectToPublicAddressAsync,
        UseProxy = false,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // The address to download, or null when the link is neither a Google Drive file link
    // nor an http(s) address. Drive links are rebuilt from the file id.
    public static string? DownloadUrl(string? link)
    {
        link = link?.Trim() ?? "";
        var drive = DriveLink.Match(link);
        if (drive.Success)
            return $"https://drive.google.com/uc?export=download&id={drive.Groups["id"].Value}";

        return Uri.TryCreate(link, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
               && !string.IsNullOrEmpty(uri.Host)
            ? uri.AbsoluteUri
            : null;
    }

    // Asks for the image without downloading it: null when the link answers with a JPEG,
    // PNG, WebP or GIF of at most maxBytes, otherwise the reason it can't be imported
    public static async Task<string?> CheckAsync(string link, int maxBytes)
    {
        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            using var res = await SendAsync(link, cts.Token);
            return res == null ? "The image link isn't a web address." : ResponseError(res, link, maxBytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return ReachError(ex);
        }
    }

    // Downloads the image: its bytes and the file extension to store it under, or the reason it failed
    public static async Task<(byte[]? Bytes, string? Extension, string? Error)> DownloadAsync(string link, int maxBytes)
    {
        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            using var res = await SendAsync(link, cts.Token);
            if (res == null)
                return (null, null, "The image link isn't a web address.");

            var error = ResponseError(res, link, maxBytes);
            if (error != null)
                return (null, null, error);

            // Content-Length can be missing or wrong, so the size is checked while reading too
            await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, cts.Token)) > 0)
            {
                if (ms.Length + read > maxBytes)
                    return (null, null, TooBig(maxBytes));
                ms.Write(buffer, 0, read);
            }

            var bytes = ms.ToArray();
            var extension = SniffExtension(bytes);
            return extension == null
                ? (null, null, "The link didn't return a JPEG, PNG, WebP or GIF image.")
                : (bytes, extension, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            return (null, null, ReachError(ex));
        }
    }

    private static async Task<HttpResponseMessage?> SendAsync(string link, CancellationToken token)
    {
        var url = DownloadUrl(link);
        if (url == null)
            return null;

        using var msg = new HttpRequestMessage(HttpMethod.Get, url);
        msg.Headers.Accept.ParseAdd("image/*");
        return await Http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token);
    }

    // Null when the response is an image the import can store
    private static string? ResponseError(HttpResponseMessage res, string link, int maxBytes)
    {
        var isDrive = DriveLink.IsMatch(link.Trim());
        var type = res.Content.Headers.ContentType?.MediaType ?? "";

        if (!res.IsSuccessStatusCode)
            return isDrive ? DriveNotShared : $"The image link answered with an error ({(int)res.StatusCode}).";
        if (isDrive && type.Contains("html", StringComparison.OrdinalIgnoreCase))
            return DriveNotShared;
        if (!ImageTypes.ContainsKey(type))
            return type.Length == 0
                ? "The link isn't a JPEG, PNG, WebP or GIF image."
                : $"The link isn't a JPEG, PNG, WebP or GIF image (it's {type}).";
        if (res.Content.Headers.ContentLength > maxBytes)
            return TooBig(maxBytes);

        return null;
    }

    private static string TooBig(int maxBytes) =>
        $"The image is over {maxBytes / 1_000_000} MB. Use a smaller copy.";

    private static string ReachError(Exception ex) => ex is OperationCanceledException
        ? "The image link took too long to answer."
        : "The image couldn't be downloaded from that link.";

    // The stored extension comes from the file's own first bytes, so the content type can't lie
    private static string? SniffExtension(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ".jpg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G')
            return ".png";
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8')
            return ".gif";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return ".webp";
        return null;
    }

    // Resolves the host and connects only when every address is on the public internet
    // (checked on each connection, so redirects and DNS changes are covered too)
    private static async ValueTask<Stream> ConnectToPublicAddressAsync(
        SocketsHttpConnectionContext context, CancellationToken token)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
        if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a)))
            throw new HttpRequestException($"{context.DnsEndPoint.Host} isn't a public internet address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, token);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address))
            return false;

        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127
                     || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)    // carrier-grade NAT
                     || (b[0] == 169 && b[1] == 254)                   // link-local, cloud metadata
                     || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                     || (b[0] == 192 && b[1] == 168)
                     || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                     || (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                     || b[0] >= 224);                                  // multicast and reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.Equals(IPAddress.IPv6Any)
                     || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                     || (b[0] & 0xFE) == 0xFC                          // unique local fc00::/7
                     || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B)   // NAT64
                     || (b[0] == 0x20 && b[1] == 0x02));               // 6to4
        }

        return false;
    }
}
