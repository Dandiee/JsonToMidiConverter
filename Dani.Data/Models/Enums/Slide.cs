namespace Dani.Data.Models.Enums;

// Byte Layout: [0 0 0] [0 0]  [0 0 0]
//              Unused  Origin Motion

[Flags]
public enum SlideFlags : byte
{
    None = 0,

    // --- GROUP A: The "Motion" (Bits 0-3) ---
    // Using distinct bits allows you to combine them if needed, 
    // or just treat them as mutually exclusive IDs.
    Upwards = 1 << 0, // 1
    Downwards = 1 << 1, // 2
    Shift = 1 << 2, // 4
    Legato = 1 << 3, // 8

    // --- GROUP B: The "Origin" (Bits 4-5) ---
    // These act as modifiers to the motions above.
    FromBelow = 1 << 4, // 16
    FromAbove = 1 << 5, // 32
}