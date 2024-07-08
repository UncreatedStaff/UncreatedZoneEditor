using System.Text.Json.Serialization;

namespace Uncreated.ZoneEditor;

public sealed class UncreatedZoneEditorConfig : IDefaultable
{
    [JsonPropertyName("hello_property")]
    public string? HelloProperty { get; set; }
    public void SetDefaults()
    {
        HelloProperty = "hello";
    }
}
