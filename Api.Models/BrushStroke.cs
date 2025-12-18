using Api.Models.Enums;

namespace Api.Models;

public class BrushStroke
{
    public Direction Direction { get; set; }
    public short Duration { get; set; }
    public float Shift { get; set; } // there's a record with value "28.000001" maybe we can just normalize the data
}