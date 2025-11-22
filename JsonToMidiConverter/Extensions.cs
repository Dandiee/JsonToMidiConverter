using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

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

    public static TMidiEvent As<TMidiEvent>(this TimedEvent timedEvent) 
        where TMidiEvent : MidiEvent
    {
        if (timedEvent.Event is TMidiEvent typedEvt)
        {
            return typedEvt;
        }

        throw new InvalidCastException($"Cannot cast event of type {timedEvent.GetType().Name} to {typeof(TMidiEvent).Name}");
    }
}