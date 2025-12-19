using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Api.Models;
using Api.Models.Enums;
using Persistence.Models.Enums;

namespace Persistence.Models;


public sealed class Beat : Poolable<Beat>
{
    public Beat() { }

    public List<Note> Notes { get; set; } = [];
    public Bend? Tremolo { get; set; }
    public ChordStroke? Stroke { get; set; }

    public MusicalFraction Duration { get; set; }

    // 19 bit
    public Harmonic Harmonic { get; set; }                      // 7 [3 bit]
    public Vibrato Vibrato { get; set; }                        // 3 [2 bit]
    public Technique Technique { get; set; }                    // 4 [2 bit]
    public Spanner BeamSpan { get; set; }                       // 3 [2 bit]
    public Direction PickDirection { get; set; }                // 3 [2 bit]
    public Dot Dots { get; set; }                               // 3 [2 bit]
    public GradualVelocity GradualVelocity { get; set; }        // 3 [2 bit]
    public Octave Octave { get; set; }                          // 3 [2 bit]
    public Spanner TupletSpan { get; set; }                     // 3 [2 bit]
    
    // 3 bit
    public bool PalmMute { get; set; }                          // [1 bit]
    public bool LetRing { get; set; }                           // [1 bit]
    public bool Rest { get; set; }                              // [1 bit]

    // 8 bit
    public byte TupletDenominator { get; set; }                 // [8 bit]


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(Span<byte> buffer, ref int cursor)
    {
        ulong hash = 0;

        // --- Build Hash ---
        hash |= Duration.Nominator;                        // Bits 0-7
        hash |= (ulong)Duration.Denominator    <<  8;      // Bits 8-15
        hash |= (ulong)TupletDenominator       << 16;      // Bits 16-23

        // Note Count (3 bits)
        hash |= (ulong)((Notes?.Count ?? 0) & 0b_111) << 24; // Bits 24-26
        
        hash |= (PalmMute ? 1UL : 0UL)           << 27;      // Bit 27
        hash |= (LetRing  ? 1UL : 0UL)           << 28;      // Bit 28
        hash |= (Rest     ? 1UL : 0UL)           << 29;      // Bit 29

        hash |= ((ulong)Harmonic        & 0b_111) << 30;     // Bits 30-32
        hash |= ((ulong)Vibrato         & 0b_011) << 33;     // Bits 33-34
        hash |= ((ulong)Technique       & 0b_011) << 35;     // Bits 35-36
        hash |= ((ulong)BeamSpan        & 0b_011) << 37;     // Bits 37-38
        hash |= ((ulong)PickDirection   & 0b_011) << 39;     // Bits 39-40
        hash |= ((ulong)Dots            & 0b_011) << 41;     // Bits 41-42
        hash |= ((ulong)GradualVelocity & 0b_011) << 43;     // Bits 43-44
        hash |= ((ulong)Octave          & 0b_011) << 45;     // Bits 45-46
        hash |= ((ulong)TupletSpan      & 0b_011) << 47;     // Bits 47-48

        // --- Write Buffer ---
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(cursor), hash);
        cursor += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        // --- Read Buffer ---
        ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(cursor));
        cursor += 8;

        // --- Unpack Properties ---
        byte durNom       = (byte)((hash)       & 0xFF);
        byte durDen       = (byte)((hash >>  8) & 0xFF);
        Duration          = new MusicalFraction(durNom, durDen);
        
        TupletDenominator = (byte)((hash >> 16) & 0xFF);         // Bits 16-23

        int noteCount     = (int) ((hash >> 24) & 0b_111);       // Bits 24-26
        Notes             = new List<Note>(noteCount);           // Init capacity

        PalmMute          = (hash & (1UL << 27)) != 0;           // Bit 27
        LetRing           = (hash & (1UL << 28)) != 0;           // Bit 28
        Rest              = (hash & (1UL << 29)) != 0;           // Bit 29

        Harmonic          = (Harmonic)       ((hash >> 30) & 0b_111); // Bits 30-32
        Vibrato           = (Vibrato)        ((hash >> 33) & 0b_011); // Bits 33-34
        Technique         = (Technique)      ((hash >> 35) & 0b_011); // Bits 35-36
        BeamSpan          = (Spanner)        ((hash >> 37) & 0b_011); // Bits 37-38
        PickDirection     = (Direction)      ((hash >> 39) & 0b_011); // Bits 39-40
        Dots              = (Dot)            ((hash >> 41) & 0b_011); // Bits 41-42
        GradualVelocity   = (GradualVelocity)((hash >> 43) & 0b_011); // Bits 43-44
        Octave            = (Octave)         ((hash >> 45) & 0b_011); // Bits 45-46
        TupletSpan        = (Spanner)        ((hash >> 47) & 0b_011); // Bits 47-48
    }
}