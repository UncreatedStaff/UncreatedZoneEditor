using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Uncreated.ZoneEditor.VehicleBays;

public class VehicleBay
{
    [JsonPropertyName("pos_x")]
    public float PositionX { get; set; }

    [JsonPropertyName("pos_y")]
    public float PositionY { get; set; }

    [JsonPropertyName("pos_z")]
    public float PositionZ { get; set; }

    [JsonPropertyName("rot_x")]
    public float RotationX { get; set; }

    [JsonPropertyName("rot_y")]
    public float RotationY { get; set; }

    [JsonPropertyName("rot_z")]
    public float RotationZ { get; set; }

    [JsonPropertyName("creator")]
    public ulong Creator { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("faction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Faction { get; set; }

    [JsonPropertyName("vehicle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid Vehicle { get; set; }
}

[JsonSerializable(typeof(VehicleBay))]
[JsonSerializable(typeof(List<VehicleBay>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class VehicleBaySerializeContext : JsonSerializerContext;