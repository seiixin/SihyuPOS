using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Result returned by <see cref="OpenFoodFactsService.LookupAsync"/>.
    /// <para>Check <see cref="Status"/> first before reading the other fields.</para>
    /// </summary>
    public sealed class OFFLookupResult
    {
        public enum LookupStatus
        {
            /// <summary>Product found and fields populated.</summary>
            Found,
            /// <summary>Barcode not in the OFF database.</summary>
            NotFound,
            /// <summary>Network unreachable, DNS failure, etc.</summary>
            Offline,
            /// <summary>Request took longer than the configured timeout.</summary>
            Timeout,
            /// <summary>Any other non-success HTTP or parse error.</summary>
            Error
        }

        public LookupStatus Status        { get; init; }
        public string?      ProductName   { get; init; }
        public string?      Brand         { get; init; }
        public string?      Category      { get; init; }
        /// <summary>Public thumbnail URL from OFF (may be null).</summary>
        public string?      ImageUrl      { get; init; }

        public bool IsFound => Status == LookupStatus.Found;

        // ── Convenience factories ────────────────────────────────────────────
        public static OFFLookupResult NotFoundResult   => new() { Status = LookupStatus.NotFound  };
        public static OFFLookupResult OfflineResult    => new() { Status = LookupStatus.Offline   };
        public static OFFLookupResult TimeoutResult    => new() { Status = LookupStatus.Timeout   };
        public static OFFLookupResult ErrorResult      => new() { Status = LookupStatus.Error     };
    }

    /// <summary>
    /// Calls the Open Food Facts v2 REST API to look up a product barcode.
    /// Falls back gracefully when the device is offline or the call times out.
    /// </summary>
    public static class OpenFoodFactsService
    {
        // Only request the fields we actually use to keep the payload small.
        private const string FieldsParam =
            "product_name,brands,categories_tags,image_front_small_url";

        private const int TimeoutSeconds = 5;

        // One shared HttpClient for the process lifetime — avoids socket exhaustion.
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
            DefaultRequestHeaders =
            {
                { "User-Agent", "SihyuPOS/1.0 (contact@sihyupos.local)" }
            }
        };

        /// <summary>
        /// Looks up <paramref name="barcode"/> on Open Food Facts.
        /// Never throws — all error cases are represented as <see cref="OFFLookupResult.LookupStatus"/> values.
        /// </summary>
        public static async Task<OFFLookupResult> LookupAsync(
            string barcode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return OFFLookupResult.NotFoundResult;

            string url =
                $"https://world.openfoodfacts.org/api/v2/product/{Uri.EscapeDataString(barcode.Trim())}.json" +
                $"?fields={FieldsParam}";

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

                using var response = await _http.GetAsync(url, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    // 404 means barcode simply not in OFF
                    return response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? OFFLookupResult.NotFoundResult
                        : OFFLookupResult.ErrorResult;
                }

                string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                return ParseResponse(json);
            }
            catch (OperationCanceledException)
            {
                return OFFLookupResult.TimeoutResult;
            }
            catch (HttpRequestException)
            {
                // No network, DNS failure, etc.
                return OFFLookupResult.OfflineResult;
            }
            catch
            {
                return OFFLookupResult.ErrorResult;
            }
        }

        // ── JSON parser ──────────────────────────────────────────────────────
        private static OFFLookupResult ParseResponse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // OFF returns { "status": 0 } when the barcode is not found.
                if (root.TryGetProperty("status", out var statusEl) &&
                    statusEl.ValueKind == JsonValueKind.Number &&
                    statusEl.GetInt32() == 0)
                    return OFFLookupResult.NotFoundResult;

                if (!root.TryGetProperty("product", out var product))
                    return OFFLookupResult.NotFoundResult;

                string? name     = GetString(product, "product_name");
                string? brand    = GetString(product, "brands");
                string? imageUrl = GetString(product, "image_front_small_url");

                // categories_tags is an array like ["en:beverages", "en:dairy"]
                string? category = null;
                if (product.TryGetProperty("categories_tags", out var tags) &&
                    tags.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tags.EnumerateArray())
                    {
                        string? t = tag.GetString();
                        if (t == null) continue;
                        // Strip the "en:" prefix and use the first tag as a category hint
                        int colon = t.IndexOf(':');
                        string tagValue = colon >= 0 ? t[(colon + 1)..] : t;
                        category = Capitalize(tagValue.Replace('-', ' '));
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(name))
                    return OFFLookupResult.NotFoundResult;

                return new OFFLookupResult
                {
                    Status      = OFFLookupResult.LookupStatus.Found,
                    ProductName = name.Trim(),
                    Brand       = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
                    Category    = category,
                    ImageUrl    = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
                };
            }
            catch
            {
                return OFFLookupResult.ErrorResult;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string? GetString(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s
            : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
