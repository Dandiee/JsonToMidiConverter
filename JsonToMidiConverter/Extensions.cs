using Melanchall.DryWetMidi.Common;

namespace JsonToMidiConverter;

public static class Extensions
{
    public static SevenBitNumber To7(this int i) => (SevenBitNumber)i;
    public static FourBitNumber To4(this int i) => (FourBitNumber)i;

    public static Slide ToSlide(this string str) => str switch
    {
        "upwards" => Slide.Upwards,
        "downwards" => Slide.Downwards,
        "shift" => Slide.Shift,
        "legato" => Slide.Legato,

        _ => Slide.None,
    };
}