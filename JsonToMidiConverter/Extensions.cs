using Melanchall.DryWetMidi.Common;

namespace JsonToMidiConverter;

public static class Extensions
{
    public static SevenBitNumber To7(this int i) => (SevenBitNumber)i;
    public static FourBitNumber To4(this int i) => (FourBitNumber)i;
}