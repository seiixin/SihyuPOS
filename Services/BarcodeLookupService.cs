using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SihyuPOSPayroll.Services
{
    public class BarcodeLookupResult
    {
        public string ProductName { get; set; } = string.Empty;
        public string? Brand      { get; set; }
    }

    public static class BarcodeLookupService
    {
        private static readonly object _lock = new();
        private static Dictionary<string, BarcodeLookupResult>? _cache;
        private static bool _loaded;
        private static bool _loadFailed;

        public static string JsonPath
        {
            get
            {
                // 1) In AppContext.BaseDirectory (deployed output)
                string p1 = Path.Combine(AppContext.BaseDirectory, "assets", "ph_grocery_starter.json");
                if (File.Exists(p1)) return p1;
                // 2) Project source (during development)
                string? baseDir = Path.GetDirectoryName(AppContext.BaseDirectory);
                while (baseDir != null)
                {
                    string probe = Path.Combine(baseDir, "assets", "ph_grocery_starter.json");
                    if (File.Exists(probe)) return probe;
                    baseDir = Path.GetDirectoryName(baseDir);
                }
                // 3) Explicit working folder copy
                string p2 = Path.Combine(Directory.GetCurrentDirectory(), "assets", "ph_grocery_starter.json");
                if (File.Exists(p2)) return p2;
                return p1;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_lock)
            {
                if (_loaded || _loadFailed) return;
                try
                {
                    string path = JsonPath;
                    if (!File.Exists(path))
                    {
                        _loadFailed = true;
                        _cache = new Dictionary<string, BarcodeLookupResult>();
                        return;
                    }
                    using var fs = File.OpenRead(path);
                    using var doc = JsonDocument.Parse(fs);
                    var dict = new Dictionary<string, BarcodeLookupResult>(StringComparer.Ordinal);
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        try
                        {
                            if (!el.TryGetProperty("barcode", out var bcProp)) continue;
                            string? bc = bcProp.GetString()?.Trim();
                            if (string.IsNullOrWhiteSpace(bc)) continue;

                            if (!el.TryGetProperty("product_name", out var nameProp)) continue;
                            string? name = nameProp.GetString()?.Trim();
                            if (string.IsNullOrWhiteSpace(name)) continue;

                            string? brand = null;
                            if (el.TryGetProperty("brand", out var brandProp))
                                brand = brandProp.GetString()?.Trim();

                            if (!dict.ContainsKey(bc))
                                dict[bc] = new BarcodeLookupResult { ProductName = name, Brand = brand };
                        }
                        catch
                        {
                            // skip malformed individual entries, keep loading the rest
                        }
                    }
                    _cache = dict;
                    _loaded = true;
                }
                catch
                {
                    _loadFailed = true;
                    _cache ??= new Dictionary<string, BarcodeLookupResult>();
                }
            }
        }

        /// <summary>Looks up barcode in the local ph_grocery_starter.json dataset.
        /// Returns null if not found — NO exception / popup is ever raised.</summary>
        public static BarcodeLookupResult? Lookup(string barcode)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            if (_cache!.TryGetValue(barcode.Trim(), out var r))
                return r;
            return null;
        }

        public static int CacheCount
        {
            get { EnsureLoaded(); return _cache!.Count; }
        }
    }
}
