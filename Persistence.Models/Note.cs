using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Api.Models;
using Api.Models.Enums;
using Persistence.Models.Enums;

namespace Persistence.Models;

public sealed class Note : Poolable<Note>
{
    public Bend? Bend { get; set; } // gotta do something with this

    public MusicalFraction Tremolo { get; set; }

    // Observed unique values
    // "-6,-5,-4,-3,-2,-1.5,-1,-0.5,0,0.5,1,1.5,2,2.5,3,3.5,4,4.5,5,5.5,6,7,8"
    public sbyte DoubledString { get; set; } // just double it to get float

    // Observed unique values
    // "-63,-60,-42,-41,-40,-39,-38,-37,-36,-35,-34,-33,-32,-31,-30,-29,-28,-27,-26,-25,-24,-23,-22,-21,-20,-19,-18,-17,-16,-15,-14,-13,-12,-11,-10,-9,-8,-7,-6,-5,-4,-3,-2,-1,0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,117,118,119,120,122,123,124,126,127"
    public sbyte Fret { get; set; } // fits nicely in sbyte

    // Observed unique values
    // "-1,0,1,2,2.4,2.7,3,3.2,4,4.4,4.7,5,5.2,5.7,5.8,6,6.2,7,8,8.2,8.4,9,9.6,10,11,11.8,12,13,14,14.7,15,16,17,18,19,19.6,20,21,21.7,22,23,24,26,28,29,35,40"
    public byte HarmonicFretIndex { get; set; } // use the included lookup table
    

    // 24 bit for bytes
    public SlideFlags Slides { get; set; }          // 32 [5 bit]
    public Velocity Velocity { get; set; }          // 8 [3 bit]
    public Harmonic Harmonic { get; set; }          // 7 [3 bit]
    public Accent Accentuated { get; set; }         // 3 [2 bit]
    public Vibrato Vibrato { get; set; }            // 3 [2 bit]
    public Legato Legato { get; set; }              // 3 [2 bit]
    public GraceNote Grace { get; set; }            // 3 [2 bit]
                                                    // 19 bit for enums

    public bool Tie { get; set; }                   // 1 [1 bit]
    public bool Rest { get; set; }                  // 1 [1 bit]
    public bool Staccato { get; set; }              // 1 [1 bit]
    public bool Dead { get; set; }                  // 1 [1 bit]
    public bool Ghost { get; set; }                 // 1 [1 bit]
    
    // This field has zero value,
    // but fucks up the byte alignment, so commented it satys
    // public bool LeftFingering { get; set; }      
                                                    // 6 bit for flags


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(Span<byte> buffer, ref int cursor)
    {
        ulong hash = 0;

        // --- Build the Hash (CPU Register) ---
        hash |= (byte)DoubledString;                    // Bits 0-7
        hash |= (ulong)(byte)Fret               << 8;   // Bits 8-15
        hash |= (ulong)HarmonicFretIndex        << 16;  // Bits 16-23

        hash |= ((ulong)Slides      & 0b_11111) << 24;  // Bits 24-28
        hash |= ((ulong)Velocity    & 0b_00111) << 29;  // Bits 29-31
        hash |= ((ulong)Harmonic    & 0b_00111) << 32;  // Bits 32-34
        hash |= ((ulong)Accentuated & 0b_00011) << 35;  // Bits 35-36
        hash |= ((ulong)Vibrato     & 0b_00011) << 37;  // Bits 37-38
        hash |= ((ulong)Legato      & 0b_00011) << 39;  // Bits 39-40
        hash |= ((ulong)Grace       & 0b_00011) << 41;  // Bits 41-42

        hash |= (Tie      ? 1UL : 0UL)          << 43;  // Bit 43
        hash |= (Rest     ? 1UL : 0UL)          << 44;  // Bit 44
        hash |= (Staccato ? 1UL : 0UL)          << 45;  // Bit 45
        hash |= (Dead     ? 1UL : 0UL)          << 46;  // Bit 46
        hash |= (Ghost    ? 1UL : 0UL)          << 47;  // Bit 47

        // Tremolo (16 bits) - Handle nulls
        hash |= (ulong)Tremolo.Nominator        << 48; // Bits 48-55
        hash |= (ulong)Tremolo.Denominator      << 56; // Bits 56-63

        // --- Write to Buffer (One Instruction) ---
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(cursor), hash);
        cursor += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Read(ReadOnlySpan<byte> buffer, ref int cursor)
    {
        // --- Read from Buffer ---
        ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(cursor));
        cursor += 8;

        // --- Unpack Properties ---
        DoubledString     = (sbyte) (hash);                       // Bits 0-7
        Fret              = (sbyte)((hash >>  8) & 0xFF);         // Bits 8-15
        HarmonicFretIndex = (byte) ((hash >> 16) & 0xFF);         // Bits 16-23

        Slides            = (SlideFlags) ((hash >> 24) & 0b_11111); // Bits 24-28
        Velocity          = (Velocity)   ((hash >> 29) & 0b_00111); // Bits 29-31
        Harmonic          = (Harmonic)   ((hash >> 32) & 0b_00111); // Bits 32-34
        Accentuated       = (Accent)     ((hash >> 35) & 0b_00011); // Bits 35-36
        Vibrato           = (Vibrato)    ((hash >> 37) & 0b_00011); // Bits 37-38
        Legato            = (Legato)     ((hash >> 39) & 0b_00011); // Bits 39-40
        Grace             = (GraceNote)  ((hash >> 41) & 0b_00011); // Bits 41-42

        Tie               = (hash & (1UL << 43)) != 0;
        Rest              = (hash & (1UL << 44)) != 0;
        Staccato          = (hash & (1UL << 45)) != 0;
        Dead              = (hash & (1UL << 46)) != 0;
        Ghost             = (hash & (1UL << 47)) != 0;

        // Unpack Tremolo
        byte tNom = (byte)((hash >> 48) & 0xFF);
        byte tDen = (byte)((hash >> 56) & 0xFF);
        Tremolo = new MusicalFraction(tNom, tDen);
    }
}