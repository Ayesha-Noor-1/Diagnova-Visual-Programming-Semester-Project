using System.Net.Http.Headers;
using System.Text.Json;
using Diagnova.Models;

namespace Diagnova.Services;

public sealed class EmergencyPlacesService
{
    private static readonly string[] OverpassEndpoints =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
    ];

    private readonly HttpClient _http;
    private readonly ILogger<EmergencyPlacesService> _logger;

    public EmergencyPlacesService(HttpClient http, ILogger<EmergencyPlacesService> logger)
    {
        _http = http;
        _logger = logger;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Diagnova", "1.0"));
        }
    }

    public async Task<IReadOnlyList<EmergencyFacilityClientDto>> FindNearbyHospitalsAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        foreach (var endpoint in OverpassEndpoints)
        {
            var results = await QueryOverpassAsync(endpoint, latitude, longitude, cancellationToken);
            if (results.Count > 0)
                return results;
        }

        var nominatim = await QueryNominatimAsync(latitude, longitude, cancellationToken);
        if (nominatim.Count > 0)
            return nominatim;

        _logger.LogWarning(
            "No hospitals found within search radius for {Lat},{Lng}. OSM data may be sparse in this area.",
            latitude, longitude);

        return
        [
            new EmergencyFacilityClientDto
            {
                Name = "Search hospitals on Google Maps",
                Address = "Opens map search near your GPS location",
                Latitude = latitude,
                Longitude = longitude,
                DistanceKm = 0,
                Notes = "https://www.google.com/maps/search/hospitals/@" +
                        $"{latitude:F6},{longitude:F6},14z",
            },
        ];
    }

    private async Task<IReadOnlyList<EmergencyFacilityClientDto>> QueryOverpassAsync(
        string endpoint,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var query = $"""
            [out:json][timeout:25];
            (
              node["amenity"="hospital"](around:50000,{latitude:F6},{longitude:F6});
              way["amenity"="hospital"](around:50000,{latitude:F6},{longitude:F6});
              relation["amenity"="hospital"](around:50000,{latitude:F6},{longitude:F6});
              node["healthcare"="hospital"](around:50000,{latitude:F6},{longitude:F6});
              node["amenity"="clinic"](around:50000,{latitude:F6},{longitude:F6});
              way["amenity"="clinic"](around:50000,{latitude:F6},{longitude:F6});
              node["amenity"="doctors"]["healthcare"](around:30000,{latitude:F6},{longitude:F6});
              node["emergency"="yes"](around:50000,{latitude:F6},{longitude:F6});
            );
            out center tags 12;
            """;

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["data"] = query,
            });

            using var response = await _http.PostAsync(endpoint, content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Overpass {Endpoint} HTTP {Status}", endpoint, response.StatusCode);
                return Array.Empty<EmergencyFacilityClientDto>();
            }

            return ParseOverpassElements(json, latitude, longitude);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Overpass request failed for {Endpoint}", endpoint);
            return Array.Empty<EmergencyFacilityClientDto>();
        }
    }

    private List<EmergencyFacilityClientDto> ParseOverpassElements(
        string json,
        double latitude,
        double longitude)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("elements", out var elements))
            return [];

        var list = new List<EmergencyFacilityClientDto>();
        foreach (var el in elements.EnumerateArray())
        {
            if (!el.TryGetProperty("tags", out var tags))
                continue;

            var name = ResolveFacilityName(tags);
            if (string.IsNullOrWhiteSpace(name))
                name = "Medical facility";

            if (!TryGetCoordinates(el, out var lat, out var lon))
                continue;

            var address = BuildAddress(tags);
            var dist = GeoUtils.DistanceKm(latitude, longitude, lat, lon);
            var emergency = tags.TryGetProperty("emergency", out var em) &&
                            string.Equals(em.GetString(), "yes", StringComparison.OrdinalIgnoreCase);

            list.Add(new EmergencyFacilityClientDto
            {
                Name = name.Trim(),
                Address = address,
                Latitude = lat,
                Longitude = lon,
                DistanceKm = Math.Round(dist, 1),
                Notes = emergency
                    ? "Emergency services indicated on OpenStreetMap."
                    : "From OpenStreetMap — call ahead to confirm ER availability.",
            });
        }

        return list
            .OrderBy(f => f.DistanceKm)
            .DistinctBy(f => $"{f.Name}|{f.Latitude:F4}|{f.Longitude:F4}")
            .Take(8)
            .ToList();
    }

    private static string ResolveFacilityName(JsonElement tags)
    {
        foreach (var key in new[] { "name", "name:en", "operator", "brand", "amenity", "healthcare" })
        {
            if (tags.TryGetProperty(key, out var el))
            {
                var v = el.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
        }
        return string.Empty;
    }

    private static string BuildAddress(JsonElement tags)
    {
        if (tags.TryGetProperty("addr:full", out var full))
        {
            var v = full.GetString();
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }

        var parts = new List<string>();
        foreach (var key in new[] { "addr:housenumber", "addr:street", "addr:city", "addr:state", "addr:country" })
        {
            if (tags.TryGetProperty(key, out var el))
            {
                var p = el.GetString();
                if (!string.IsNullOrWhiteSpace(p))
                    parts.Add(p);
            }
        }

        return string.Join(", ", parts);
    }

    private static bool TryGetCoordinates(JsonElement el, out double lat, out double lon)
    {
        lat = lon = 0;
        if (el.TryGetProperty("lat", out var latEl) && el.TryGetProperty("lon", out var lonEl))
        {
            lat = latEl.GetDouble();
            lon = lonEl.GetDouble();
            return true;
        }

        if (el.TryGetProperty("center", out var center) &&
            center.TryGetProperty("lat", out var cLat) &&
            center.TryGetProperty("lon", out var cLon))
        {
            lat = cLat.GetDouble();
            lon = cLon.GetDouble();
            return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<EmergencyFacilityClientDto>> QueryNominatimAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        try
        {
            var url =
                $"https://nominatim.openstreetmap.org/search?" +
                $"format=json&limit=8&amenity=hospital&lat={latitude:F6}&lon={longitude:F6}" +
                $"&bounded=1&viewbox={longitude - 0.45:F4},{latitude + 0.35:F4},{longitude + 0.45:F4},{latitude - 0.35:F4}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<EmergencyFacilityClientDto>();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<EmergencyFacilityClientDto>();

            var list = new List<EmergencyFacilityClientDto>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("lat", out var latEl) ||
                    !item.TryGetProperty("lon", out var lonEl))
                    continue;

                var lat = double.Parse(latEl.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                var lon = double.Parse(lonEl.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                var name = item.TryGetProperty("display_name", out var dn)
                    ? (dn.GetString()?.Split(',').FirstOrDefault()?.Trim() ?? "Hospital")
                    : "Hospital";

                list.Add(new EmergencyFacilityClientDto
                {
                    Name = name,
                    Address = item.TryGetProperty("display_name", out var full) ? full.GetString() ?? "" : "",
                    Latitude = lat,
                    Longitude = lon,
                    DistanceKm = Math.Round(GeoUtils.DistanceKm(latitude, longitude, lat, lon), 1),
                    Notes = "From OpenStreetMap search — verify ER by phone.",
                });
            }

            return list.OrderBy(f => f.DistanceKm).Take(8).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nominatim hospital search failed");
            return Array.Empty<EmergencyFacilityClientDto>();
        }
    }
}
