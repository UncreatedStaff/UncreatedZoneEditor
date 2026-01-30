using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Uncreated.ZoneEditor.VehicleBays;

public class VehicleBaySign
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

    [JsonPropertyName("vehicle_bay")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VehicleBayId { get; set; }
}

[JsonSerializable(typeof(VehicleBaySign))]
[JsonSerializable(typeof(List<VehicleBaySign>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class VehicleBaySignSerializeContext : JsonSerializerContext;