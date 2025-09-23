using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Linq;

namespace agapay_backend.Services
{
    public class SupabaseStorageService : ISupabaseStorageService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _supabaseUrl;
        private readonly string _serviceRoleKey;
        private readonly string _bucket;
        private readonly ILogger<SupabaseStorageService> _logger;

        public SupabaseStorageService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<SupabaseStorageService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _supabaseUrl = config["Supabase:Url"]?.TrimEnd('/') ?? throw new ArgumentNullException("Supabase:Url missing from configuration");
            _serviceRoleKey = config["Supabase:ServiceRoleKey"] ?? throw new ArgumentNullException("Supabase:ServiceRoleKey missing from configuration");
            _bucket = config["Supabase:Bucket"] ?? throw new ArgumentNullException("Supabase:Bucket missing from configuration");
            _logger = logger;
        }

        // Encode each path segment but preserve '/' separators so Supabase signs the correct path.
        private static string EncodePathSegments(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return string.Join("/", path.Split('/').Select(segment => Uri.EscapeDataString(segment)));
        }

        /// <summary>
        /// Uploads file and returns the object path (not the full public url).
        /// Example object path: "licenses/{guid}.jpg" or "licenses/{userId}/{guid}.jpg"
        /// </summary>
        public async Task<string> UploadFileAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var objectPath = string.IsNullOrWhiteSpace(folder) ? fileName : $"{folder.Trim('/')}/{fileName}";

            var encodedPath = EncodePathSegments(objectPath);
            var requestUri = $"storage/v1/object/{_bucket}/{encodedPath}";

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_supabaseUrl);

            client.DefaultRequestHeaders.Remove("apikey");
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("apikey", _serviceRoleKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);

            using var stream = file.OpenReadStream();
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");

            var response = await client.PutAsync(requestUri, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Supabase upload failed ({StatusCode}) body: {Body}", (int)response.StatusCode, body);
                throw new InvalidOperationException($"Supabase storage upload failed ({(int)response.StatusCode}): {body}");
            }

            // return object path (store this in DB). Use GetPublicUrl or GetSignedUrlAsync to preview.
            return objectPath;
        }

        public string GetPublicUrl(string objectPath)
        {
            if (string.IsNullOrWhiteSpace(objectPath)) return objectPath;
            var encodedPath = EncodePathSegments(objectPath);
            return $"{_supabaseUrl}/storage/v1/object/public/{Uri.EscapeDataString(_bucket)}/{encodedPath}";
        }

        public async Task<string?> GetSignedUrlAsync(string objectPath, int expiresInSeconds = 259200, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                _logger.LogWarning("GetSignedUrlAsync called with empty objectPath");
                return null;
            }

            var encodedPath = EncodePathSegments(objectPath);
            var requestUri = $"storage/v1/object/sign/{_bucket}/{encodedPath}";
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_supabaseUrl);
            client.DefaultRequestHeaders.Remove("apikey");
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("apikey", _serviceRoleKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);

            HttpResponseMessage response;
            try
            {
                var payloadObj = new { expiresIn = expiresInSeconds };
                var payloadJson = JsonSerializer.Serialize(payloadObj);
                using var payload = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                response = await client.PostAsync(requestUri, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while calling Supabase sign endpoint for path {Path}", objectPath);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Supabase sign url response for {Path} -> {StatusCode} : {Body}", objectPath, (int)response.StatusCode, body);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Supabase storage sign URL failed ({StatusCode}) for {Path}: {Body}", (int)response.StatusCode, objectPath, body);
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                string? rawSigned = null;
                if (doc.RootElement.TryGetProperty("signedURL", out var signedUrlElem))
                    rawSigned = signedUrlElem.GetString();
                else if (doc.RootElement.TryGetProperty("signedUrl", out signedUrlElem))
                    rawSigned = signedUrlElem.GetString();
                else if (doc.RootElement.TryGetProperty("signed_url", out signedUrlElem))
                    rawSigned = signedUrlElem.GetString();

                if (string.IsNullOrWhiteSpace(rawSigned))
                {
                    _logger.LogWarning("Supabase sign response did not contain expected keys; returning raw body for {Path}", objectPath);
                    return body;
                }

                // If Supabase returned an absolute URL, return it unchanged.
                if (Uri.IsWellFormedUriString(rawSigned, UriKind.Absolute))
                    return rawSigned;

                // Protocol-relative URL (//host/...) — assume https
                if (rawSigned.StartsWith("//"))
                    return "https:" + rawSigned;

                // Relative path returned by Supabase (starts with '/'):
                // Ensure canonical /storage/v1 prefix is present if needed.
                if (rawSigned.StartsWith("/"))
                {
                    try
                    {
                        var baseUri = new Uri(_supabaseUrl);
                        var authority = $"{baseUri.Scheme}://{baseUri.Authority}";

                        var signedPath = rawSigned;
                        if (!signedPath.StartsWith("/storage/v1", StringComparison.OrdinalIgnoreCase))
                        {
                            if (signedPath.StartsWith("/"))
                                signedPath = "/storage/v1" + signedPath;
                            else
                                signedPath = "/storage/v1/" + signedPath;
                        }

                        return $"{authority}{signedPath}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to compose absolute URL from relative signed URL; returning raw signed value");
                        return rawSigned;
                    }
                }

                // Fallback: return whatever Supabase gave us.
                return rawSigned;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Supabase sign response JSON for {Path}; returning raw body", objectPath);
                return body;
            }
        }

        public async Task DeleteFileAsync(string publicUrlOrPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(publicUrlOrPath)) return;

            // If a full public URL was provided, extract the object path after "/object/public/{bucket}/"
            string objectPath = publicUrlOrPath;
            var marker = $"/storage/v1/object/public/{_bucket}/";
            if (publicUrlOrPath.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                var idx = publicUrlOrPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                objectPath = Uri.UnescapeDataString(publicUrlOrPath.Substring(idx + marker.Length));
            }
            var qIdx = objectPath.IndexOf('?');
            if (qIdx >= 0) objectPath = objectPath.Substring(0, qIdx);

            var encodedPath = EncodePathSegments(objectPath);
            var requestUri = $"storage/v1/object/{_bucket}/{encodedPath}";
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_supabaseUrl);
            client.DefaultRequestHeaders.Remove("apikey");
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("apikey", _serviceRoleKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);

            var response = await client.DeleteAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Supabase storage delete failed ({StatusCode}) for {Path}: {Body}", (int)response.StatusCode, objectPath, body);
                throw new InvalidOperationException($"Supabase storage delete failed ({(int)response.StatusCode}): {body}");
            }
        }
    }
}
