using Api.Generators;
using Api.Models.Converters;
using Api.Models.Enums;
using Api.Models.Serialization;
using System.Text.Json.Serialization;

namespace Api.Models;

[AutoSerializeIndex]
[JsonConverter(typeof(NoteConverter))]
public sealed partial class Note : Serializable
{
    public Bend? Bend { get; set; }

    // fixed 16 bits
    public MusicalFraction Tremolo { get; set; } = MusicalFraction.Zero;

    // Observed unique values: "-6,-5,-4,-3,-2,-1.5,-1,-0.5,0,0.5,1,1.5,2,2.5,3,3.5,4,4.5,5,5.5,6,7,8"
    public sbyte DoubledString { get; set; } // just double it to get float

    // Observed unique values: "-63,-60,-42,-41,-40,-39,-38,-37,-36,-35,-34,-33,-32,-31,-30,-29,-28,-27,-26,-25,-24,-23,-22,-21,-20,-19,-18,-17,-16,-15,-14,-13,-12,-11,-10,-9,-8,-7,-6,-5,-4,-3,-2,-1,0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,117,118,119,120,122,123,124,126,127"
    public sbyte Fret { get; set; } // fits nicely in sbyte

    // Observed unique values: "-1,0,1,2,2.4,2.7,3,3.2,4,4.4,4.7,5,5.2,5.7,5.8,6,6.2,7,8,8.2,8.4,9,9.6,10,11,11.8,12,13,14,14.7,15,16,17,18,19,19.6,20,21,21.7,22,23,24,26,28,29,35,40"
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
}