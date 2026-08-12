using System;
using System.Text.Json;

namespace Sonar.Core;

public static class SimulationConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Deserializa y valida. Lanza si el JSON es inválido o rompe una invariante.</summary>
    public static SimulationConfig LoadFromJson(string json)
    {
        var config = JsonSerializer.Deserialize<SimulationConfig>(json, Options)
                     ?? throw new ArgumentException("La configuración deserializó a null.", nameof(json));
        config.Validate();
        return config;
    }
}
