using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Models.Song;

[DebuggerDisplay("B{Index} M{Measure.Index} P{Part.Index}")]
public partial class Beat
{
    public List<Nota> Notes { get; set; } = [];
    public string Velocity { get; set; } = string.Empty;
    public double Type { get; set; }
    public bool PalmMute { get; set; }
    [JsonPropertyName("duration")]
    public List<long> DurationArray { get; set; } = [];
    public bool BeamStart { get; set; }
    public bool BeamStop { get; set; }
    public bool Vibrato { get; set; }
    public Text? Text { get; set; }
    public bool LetRing { get; set; }
    public int Dots { get; set; }
    public bool Rest { get; set; }
    public bool Tapping { get; set; }
    public int Tuplet { get; set; }
    public bool TupletStart { get; set; }
    public bool TupletStop { get; set; }
    public string? GraceNote { get; set; }
    public int UpStroke { get; set; }
    public int DownStroke { get; set; }

    [JsonConverter(typeof(MarkerConverter))]
    public Marker? Chord { get; set; }
    public bool Slapping { get; set; }
    public bool Popping { get; set; }
    public string? GradualVelocity { get; set; }
    public string? VibratoWithTremoloBar { get; set; }
    public int? VibratoBar { get; set; }


    [JsonConverter(typeof(PickStrokeConverter))]
    public string? PickStroke { get; set; }

    [JsonConverter(typeof(TremoloBarConverter))]
    public Bend? TremoloBar { get; set; }
    public bool WideVibrato { get; set; }
    public BrushStroke? BrushStroke { get; set; }
    public int DownArpeggio { get; set; }
    public bool HasRasgueado { get; set; }
    public BrushStroke? Arpeggio { get; set; }
    public int UpArpeggio { get; set; }
    public object Tempo { get; set; }
    public bool Dotted { get; set; }
    public bool FadeIn { get; set; }
    public bool Harmonic { get; set; }
    public bool SemiHarmonic { get; set; }
    public bool ArtificialHarmonic { get; set; }
    public bool PinchHarmonic { get; set; }
    public bool TapHarmonic { get; set; }
    public int? WideVibratoBar { get; set; }

    public string? Golpe { get; set; }
    public bool DoubleDotted { get; set; }
    public string? OctaveClef { get; set; }

    public Beat Clone() => new()
    {
        Notes = Notes.Select(e => e.Clone()).ToList(),
        Velocity = Velocity,
        Type = Type,
        PalmMute = PalmMute,
        DurationArray = DurationArray.Select(e => e).ToList(),
        BeamStart = BeamStart,
        BeamStop = BeamStop,
        Vibrato = Vibrato,
        Text = Text?.Clone(),
        LetRing = LetRing,
        Dots = Dots,
        Rest = Rest,
        Tapping = Tapping,
        Tuplet = Tuplet,
        TupletStart = TupletStart,
        TupletStop = TupletStop,
        GraceNote = GraceNote,
        UpStroke = UpStroke,
        DownStroke = DownStroke,
        Chord = Chord?.Clone(),
        Slapping = Slapping,
        Popping = Popping,
        GradualVelocity = GradualVelocity,
        VibratoWithTremoloBar = VibratoWithTremoloBar,
        PickStroke = PickStroke,
        TremoloBar = TremoloBar?.Clone(),
        WideVibrato = WideVibrato,
        BrushStroke = BrushStroke?.Clone(),
        DownArpeggio = DownArpeggio,
        HasRasgueado = HasRasgueado,
        Arpeggio = Arpeggio?.Clone(),
        UpArpeggio = UpArpeggio,
        Tempo = Tempo,
        Dotted = Dotted,
        FadeIn = FadeIn,
        Harmonic = Harmonic,
        SemiHarmonic = SemiHarmonic,
        ArtificialHarmonic = ArtificialHarmonic,
        PinchHarmonic = PinchHarmonic,
        Golpe = Golpe,
        DoubleDotted = DoubleDotted,
        OctaveClef = OctaveClef,
        TapHarmonic = TapHarmonic,
        VibratoBar = VibratoBar,
        WideVibratoBar = WideVibratoBar
    };
}

public sealed class HarmonicData
{
    public string Type { get; set; }
    public string Note { get; set; }
    public int Shift { get; set; }
    public int? Fret { get; set; }

    public HarmonicData Clone() => new()
    {
        Type = Type,
        Note = Note,
        Shift = Shift,
        Fret = Fret
    };
}

public class BrushStroke
{
    public string Direction { get; set; }
    public int Duration { get; set; }
    public double Shift { get; set; }

    public BrushStroke Clone() => new()
    {
        Direction = Direction,
        Duration = Duration,
        Shift = Shift
    };
}

public class TremoloBarConverter : JsonConverter<Bend?>
{
    public override Bend? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize<Bend>(ref reader, options);
        }

        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            bool value = reader.GetBoolean();

            if (value)
            {
                return new Bend { LegacyFlag = reader.GetBoolean() };
            }
            return null;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, Bend? value, JsonSerializerOptions options)
    {
        // When writing back to JSON, we always write the object structure
        // unless you specifically want to write 'true' for simple cases.
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}

public class PickStrokeConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean() ? "down" : null;
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}

public class AccentuatedConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean() ? 1 : 0;
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble();
        }

        return 0;
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}

public class TremoloConverter : JsonConverter<MusicalFraction?>
{
    public override MusicalFraction? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean() ? new MusicalFraction(1, 16) : null;
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            return MusicalFractionConverter.Instance.Read(ref reader, typeToConvert, options);
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, MusicalFraction value, JsonSerializerOptions options)
        => MusicalFractionConverter.Instance.Write(writer, value, options);
}

public class MusicalFractionConverter : JsonConverter<MusicalFraction>
{
    public static readonly MusicalFractionConverter Instance = new();

    public override MusicalFraction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of array.");
        }

        // 2. Read the Numerator (First element)
        reader.Read();
        if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected numerator.");
        long numerator = reader.GetInt64();

        // 3. Read the Denominator (Second element)
        reader.Read();
        if (reader.TokenType != JsonTokenType.Number && reader.TokenType != JsonTokenType.Null) throw new JsonException("Expected denominator.");
        long denominator = reader.TokenType == JsonTokenType.Null
            ? 0
            : reader.GetInt64();

        // 4. Consume the End of Array ']'
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Expected end of array.");
        }

        return new MusicalFraction(numerator, denominator);
    }

    public override void Write(Utf8JsonWriter writer, MusicalFraction value, JsonSerializerOptions options)
    {
        // Write it back as a compact array: [1, 4]
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Numerator);
        writer.WriteNumberValue(value.Denominator);
        writer.WriteEndArray();
    }
}


public class VibratoConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean();
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble() > 0;
        }

        return false;
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}

public class MarkerConverter : JsonConverter<Marker?>
{
    public override Marker? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize<Marker>(ref reader, options);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return new Marker
            {
                Text = reader.GetString()
            };
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, Marker? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
