using System;

namespace MeltySynth
{
    internal sealed class Channel
    {
        private readonly Synthesizer synthesizer;
        private readonly bool isPercussionChannel;

        private int bankNumber;
        private int patchNumber;

        private short modulation;
        private short volume;
        private short pan;
        private short expression;
        private bool holdPedal;

        private byte reverbSend;
        private byte chorusSend;

        // engine: GM2 sound controllers (CC71-75). 64 leaves the sound as the font made it; one step is 1/16 octave
        // for the cutoff and the envelope times, 0.2 dB for the resonance. The rhythm channel does not receive them.
        private byte resonance;
        private byte releaseTime;
        private byte attackTime;
        private byte brightness;
        private byte decayTime;

        private short rpn;
        private short nrpn; // engine: the selected NRPN (-1 = none); selecting one deselects the RPN and vice versa
        private short pitchBendRange;
        private short coarseTune;
        private short fineTune;

        private float pitchBend;

        internal Channel(Synthesizer synthesizer, bool isPercussionChannel)
        {
            this.synthesizer = synthesizer;
            this.isPercussionChannel = isPercussionChannel;

            Reset();
        }

        public void Reset()
        {
            bankNumber = isPercussionChannel ? 128 : 0;
            patchNumber = 0;

            modulation = 0;
            volume = 100 << 7;
            pan = 64 << 7;
            expression = 127 << 7;
            holdPedal = false;

            reverbSend = 40;
            chorusSend = 0;

            resonance = 64;
            releaseTime = 64;
            attackTime = 64;
            brightness = 64;
            decayTime = 64;

            rpn = -1;
            nrpn = -1;
            pitchBendRange = 2 << 7;
            coarseTune = 0;
            fineTune = 8192;

            pitchBend = 0F;
        }

        public void ResetAllControllers()
        {
            modulation = 0;
            expression = 127 << 7;
            holdPedal = false;

            rpn = -1;
            nrpn = -1;

            pitchBend = 0F;
        }

        public void SetBank(int value)
        {
            bankNumber = value;

            if (isPercussionChannel)
            {
                bankNumber += 128;
            }
        }

        public void SetPatch(int value)
        {
            patchNumber = value;
        }

        public void SetModulationCoarse(int value)
        {
            modulation = (short)((modulation & 0x7F) | (value << 7));
        }

        public void SetModulationFine(int value)
        {
            modulation = (short)((modulation & 0xFF80) | value);
        }

        public void SetVolumeCoarse(int value)
        {
            volume = (short)((volume & 0x7F) | (value << 7));
        }

        public void SetVolumeFine(int value)
        {
            volume = (short)((volume & 0xFF80) | value);
        }

        public void SetPanCoarse(int value)
        {
            pan = (short)((pan & 0x7F) | (value << 7));
        }

        public void SetPanFine(int value)
        {
            pan = (short)((pan & 0xFF80) | value);
        }

        public void SetExpressionCoarse(int value)
        {
            expression = (short)((expression & 0x7F) | (value << 7));
        }

        public void SetExpressionFine(int value)
        {
            expression = (short)((expression & 0xFF80) | value);
        }

        public void SetHoldPedal(int value)
        {
            holdPedal = value >= 64;
        }

        public void SetReverbSend(int value)
        {
            reverbSend = (byte)value;
        }

        public void SetChorusSend(int value)
        {
            chorusSend = (byte)value;
        }

        public void SetSoundController(int control, int value)
        {
            if (isPercussionChannel)
            {
                return;
            }

            switch (control)
            {
                case 0x47:
                    resonance = (byte)value;
                    break;

                case 0x48:
                    releaseTime = (byte)value;
                    break;

                case 0x49:
                    attackTime = (byte)value;
                    break;

                case 0x4A:
                    brightness = (byte)value;
                    break;

                case 0x4B:
                    decayTime = (byte)value;
                    break;
            }
        }

        public void SetRpnCoarse(int value)
        {
            nrpn = -1;
            rpn = (short)((rpn & 0x7F) | (value << 7));
        }

        public void SetRpnFine(int value)
        {
            nrpn = -1;
            rpn = (short)((rpn & 0xFF80) | value);
        }

        // engine: without these, data entry meant for an NRPN went to the last RPN (a GS/XG part setup of
        // "RPN 0 = 2, then NRPN vibrato = 64" left the pitch bend range at 64 semitones)
        public void SetNrpnCoarse(int value)
        {
            rpn = -1;
            nrpn = (short)((nrpn & 0x7F) | (value << 7));
        }

        public void SetNrpnFine(int value)
        {
            rpn = -1;
            nrpn = (short)((nrpn & 0xFF80) | value);
        }

        public void DataEntryCoarse(int value)
        {
            switch (rpn)
            {
                case 0:
                    pitchBendRange = (short)((pitchBendRange & 0x7F) | (value << 7));
                    break;

                case 1:
                    fineTune = (short)((fineTune & 0x7F) | (value << 7));
                    break;

                case 2:
                    coarseTune = (short)(value - 64);
                    break;
            }
        }

        public void DataEntryFine(int value)
        {
            switch (rpn)
            {
                case 0:
                    pitchBendRange = (short)((pitchBendRange & 0xFF80) | value);
                    break;

                case 1:
                    fineTune = (short)((fineTune & 0xFF80) | value);
                    break;
            }
        }

        public void SetPitchBend(int value1, int value2)
        {
            pitchBend = (1F / 8192F) * ((value1 | (value2 << 7)) - 8192);
        }

        public bool IsPercussionChannel => isPercussionChannel;

        public int BankNumber => bankNumber;
        public int PatchNumber => patchNumber;

        public float Modulation => (50F / 16383F) * modulation;
        public float Volume => (1F / 16383F) * volume;
        public float Pan => (100F / 16383F) * pan - 50F;
        public float Expression => (1F / 16383F) * expression;
        public bool HoldPedal => holdPedal;

        public float ReverbSend => (1F / 127F) * reverbSend;
        public float ChorusSend => (1F / 127F) * chorusSend;

        public float Resonance => 0.2F * (resonance - 64); // dB
        public float ReleaseTimeFactor => TimeFactor(releaseTime);
        public float AttackTimeFactor => TimeFactor(attackTime);
        public float Brightness => 75F * (brightness - 64); // cents
        public float DecayTimeFactor => TimeFactor(decayTime);

        private static float TimeFactor(byte value) => MathF.Pow(2F, (value - 64) / 16F);

        public float PitchBendRange => (pitchBendRange >> 7) + 0.01F * (pitchBendRange & 0x7F);
        public float Tune => coarseTune + (1F / 8192F) * (fineTune - 8192);

        public float PitchBend => PitchBendRange * pitchBend;
    }
}
