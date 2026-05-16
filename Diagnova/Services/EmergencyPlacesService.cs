using System.Text.Json;
using Diagnova.Models;

namespace Diagnova.Services;

public sealed class EmergencyPlacesService
{
    private readonly HttpClient _http;
    private readonly ILogger<EmergencyPlacesService> _logger;

    public EmergencyPlacesService(HttpClient http, ILogger<EmergencyPlacesService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmergencyFacilityClientDto>> FindNearbyHospitalsAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var query = $"""
            [out:json][timeout:20];
            (
              node["amenity"="hospital"](around:25000,{latitude:F6},{longitude:F6});
              way["amenity"="hospital"](around:25000,{latitude:F6},{longitude:F6});
              node["amenity"="clinic"]["emergency"="yes"](around:25000,{latitude:F6},{longitude:F6});
            );
            out center 8;
            """;

        try
        {
            using var content = new StringContent(query);
            using var response = await _http.PostAsync(
                "https://overpass-api.de/api/interpreter",
                content,
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Overpass HTTP {Status}", response.StatusCode);
                return Array.Empty<EmergencyFacilityClientDto>();
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("elements", out var elements))
                return Array.Empty<EmergencyFacilityClientDto>();

            var list = new List<EmergencyFacilityClientDto>();
            foreach (var el in elements.EnumerateArray())
            {
                var name = el.TryGetProperty("tags", out var tags) && tags.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                double lat, lon;
                if (el.TryGetProperty("lat", out var latEl) && el.TryGetProperty("lon", out var lonEl))
                {
                    lat = latEl.GetDouble();
                    lon = lonEl.GetDouble();
                }
                else if (el.TryGetProperty("center", out var center))
                {
                    lat = center.GetProperty("lat").GetDouble();
                    lon = center.GetProperty("lon").GetDouble();
                }
                else
                {
                    continue;
                }

                var address = tags.TryGetProperty("addr:full", out var full)
                    ? full.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(address))
                {
                    var parts = new List<string>();
                    if (tags.TryGetProperty("addr:street", out var street))
                        parts.Add(street.GetString() ?? "");
                    if (tags.TryGetProperty("addr:city", out var city))
                        parts.Add(city.GetString() ?? "");
                    address = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                }

                var dist = GeoUtils.DistanceKm(latitude, longitude, lat, lon);
                list.Add(new EmergencyFacilityClientDto
                {
                    Name = name.Trim(),
                    Address = address ?? string.Empty,
                    Latitude = lat,
                    Longitude = lon,
                    DistanceKm = Math.Round(dist, 1),
                    Notes = "From OpenStreetMap — verify ER availability by phone.",
                });
            }

            return list
                .OrderBy(f => f.DistanceKm)
                .DistinctBy(f => f.Name)
                .Take(5)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Overpass hospital lookup failed");
            return Array.Empty<EmergencyFacilityClientDto>();
        }
    }
}
