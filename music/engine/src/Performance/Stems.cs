using System.Collections.Generic;

namespace Junimo.Engine.Performance
{
    /// <summary>Splitting a performance into mixer channels, each played by a synth of its own.</summary>
    public static class Stems
    {
        /// <summary>
        /// What the synth of one mixer channel receives: every note-on of a part outside the channel becomes a
        /// release (velocity 0). Nothing is removed, so all the channels' synths receive the same bytes at the same
        /// moments (on a MIDI cable that is what decides when everything arrives) and their audio lines up sample for
        /// sample.
        /// </summary>
        public static TimedMessage Keep(TimedMessage m, IReadOnlySet<int> channels)
        {
            byte[] b = m.Bytes;
            if (b.Length == 3 && b[0] >> 4 == 0x9 && b[2] != 0 && !channels.Contains(b[0] & 0x0F))
                return new TimedMessage(m.Sample, new[] { b[0], b[1], (byte)0 });
            return m;
        }
    }
}
