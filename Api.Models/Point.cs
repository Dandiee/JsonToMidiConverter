namespace Api.Models;

public class Point
{
    public float Position { get; set; } // 0 - 60
    public float Tone { get; set; } // -800 - 600
    public byte Vibrato { get; set; } // TODO: not all types have vibrato
}