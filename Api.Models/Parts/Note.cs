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
    public MusicalFraction Tremolo { get; set; } = MusicalFraction.Zero;
    public sbyte DoubledString { get; set; }
    public sbyte Fret { get; set; }
    public byte HarmonicFretIndex { get; set; }
    
    public SlideFlags Slides { get; set; }         
    public Velocity Velocity { get; set; }         
    public Harmonic Harmonic { get; set; }         
    public Accent Accentuated { get; set; }        
    public Vibrato Vibrato { get; set; }           
    public Legato Legato { get; set; }             
    public GraceNote Grace { get; set; }           
                                                   

    public bool Tie { get; set; }                  
    public bool Rest { get; set; }                 
    public bool Staccato { get; set; }             
    public bool Dead { get; set; }                 
    public bool Ghost { get; set; }                
    
    // public bool LeftFingering { get; set; }      
}