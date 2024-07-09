using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Uncreated.ZoneEditor.Data;

internal class ZoneJsonConfig : JsonConfigurationFile<ZoneJsonList>
{
    public override ZoneJsonList Default => new ZoneJsonList { Zones = [] };
    public ZoneJsonConfig(string file) : base(file) { }
    protected override void OnReload()
    {
        Configuration.Zones ??= [ ];
    }
}

internal class ZoneJsonList : SchemaConfiguration
{
    protected override string GetSchemaURI() => "https://raw.githubusercontent.com/UncreatedStaff/UncreatedZoneEditor/master/Schemas/zone_list_schema.json";
    
    [JsonPropertyName("zones")]
    public List<ZoneJsonModel> Zones { get; set; }
}

internal class ZoneJsonModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("short_name")]
    public string? ShortName { get; set; }

    [JsonPropertyName("creator")]
    public CSteamID Creator { get; set; }

    [JsonPropertyName("center_pos")]
    public Vector3 Center { get; set; }

    [JsonPropertyName("spawn_pos")]
    public Vector3 Spawn { get; set; }

    [JsonPropertyName("shape")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ZoneShape Shape { get; set; }

    [JsonPropertyName("height")]
    public float Height { get; set; }

    [JsonPropertyName("circle")]
    public ZoneJsonCircleInfo? CircleInfo { get; set; }

    [JsonPropertyName("aabb")]
    public ZoneJsonAABBInfo? AABBInfo { get; set; }

    [JsonPropertyName("polygon")]
    public ZoneJsonPolygonInfo? PolygonInfo { get; set; }
}

public class ZoneJsonCircleInfo
{
    [JsonPropertyName("radius")]
    public float Radius { get; set; }
}

public class ZoneJsonAABBInfo
{
    [JsonPropertyName("size")]
    public Vector3 Size { get; set; }
}

public class ZoneJsonPolygonInfo
{
    [JsonPropertyName("points")]
    public Vector3[] Points { get; set; } = Array.Empty<Vector3>();
}