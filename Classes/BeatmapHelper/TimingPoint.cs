using StandalonePicturator.Classes.BeatmapHelper.BeatDivisors;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace StandalonePicturator.Classes.BeatmapHelper {
    public class TimingPoint : ITextLine, IComparable<TimingPoint> {
        public double Offset { get; set; }
        public double MpB { get; set; }
        public TempoSignature Meter { get; set; }
        public SampleSet SampleSet { get; set; }
        public int SampleIndex { get; set; }
        public double Volume { get; set; }
        public bool Uninherited { get; set; }
        public bool Kiai { get; set; }
        public bool OmitFirstBarLine { get; set; }

        [JsonIgnore]
        public bool SaveWithFloatPrecision { get; set; }

        public TimingPoint(double offset, double mpb, int meter, SampleSet sampleSet, int sampleIndex, double volume, bool uninherited, bool kiai, bool omitFirstBarLine) {
            Offset = offset;
            MpB = mpb;
            Meter = new TempoSignature(meter);
            SampleSet = sampleSet;
            SampleIndex = sampleIndex;
            Volume = volume;
            Uninherited = uninherited;
            Kiai = kiai;
            OmitFirstBarLine = omitFirstBarLine;
        }

        public TimingPoint(double offset, double mpb, TempoSignature meter, SampleSet sampleSet, int sampleIndex, double volume, bool uninherited, bool kiai, bool omitFirstBarLine)
        {
            Offset = offset;
            MpB = mpb;
            Meter = meter;
            SampleSet = sampleSet;
            SampleIndex = sampleIndex;
            Volume = volume;
            Uninherited = uninherited;
            Kiai = kiai;
            OmitFirstBarLine = omitFirstBarLine;
        }

        /* ----- ĐÃ COMMENT LẠI PHẦN ĐỌC TỪ GAME EDITOR -----
        public TimingPoint(Editor_Reader.ControlPoint cp) {
            MpB = cp.BeatLength;
            Offset = cp.Offset;
            SampleIndex = cp.CustomSamples;
            SampleSet = (SampleSet)cp.SampleSet;
            Meter = new TempoSignature(cp.TimeSignature);
            Volume = cp.Volume;
            Kiai = (cp.EffectFlags & 1) > 0;
            OmitFirstBarLine = (cp.EffectFlags & 8) > 0;
            Uninherited = cp.TimingChange;
        }

        public static explicit operator TimingPoint(Editor_Reader.ControlPoint cp) {
            return new TimingPoint(cp);
        }
        --------------------------------------------------- */

        public TimingPoint(string line) {
            SetLine(line);
        }

        public TimingPoint()
        {
            MpB = 60000;
            Offset = 0;
            Meter = new TempoSignature(4,4);
            SampleSet = new SampleSet();
            SampleIndex = 0;
            Volume = 100;
            Uninherited = false;
            Kiai = false;
            OmitFirstBarLine = false;
        }

        public string GetLine() {
            int style = MathHelper.GetIntFromBitArray(new BitArray(new[] { Kiai, false, false, OmitFirstBarLine }));
            return $"{Offset.ToInvariant()},{MpB.ToInvariant()},{Meter.TempoNumerator.ToInvariant()},{SampleSet.ToIntInvariant()},{SampleIndex.ToInvariant()},{(SaveWithFloatPrecision ? Volume.ToInvariant() : Volume.ToRoundInvariant())},{Convert.ToInt32(Uninherited).ToInvariant()},{style.ToInvariant()}";
        }

        public void SetLine(string line) {
            string[] values = line.Split(',');

            if (FileFormatHelper.TryParseDouble(values[0], out double offset))
                Offset = offset;
            else throw new Exception("Failed to parse offset of timing point: " + line);

            if (FileFormatHelper.TryParseDouble(values[1], out double mpb))
                MpB = mpb;
            else throw new Exception("Failed to parse milliseconds per beat of timing point: " + line);

            if (FileFormatHelper.TryParseInt(values[2], out int meter))
                Meter = new TempoSignature(meter);
            else throw new Exception("Failed to parse meter of timing point: " + line);

            if (Enum.TryParse(values[3], out SampleSet ss))
                SampleSet = ss;
            else throw new Exception("Failed to parse sampleset of timing point: " + line);

            if (FileFormatHelper.TryParseInt(values[4], out int ind))
                SampleIndex = ind;
            else throw new Exception("Failed to parse sample index of timing point: " + line);

            if (FileFormatHelper.TryParseDouble(values[5], out double vol))
                Volume = vol;
            else throw new Exception("Failed to parse volume of timing point: " + line);

            Uninherited = values[6] == "1";

            if (values.Length <= 7) return;
            if (FileFormatHelper.TryParseInt(values[7], out int style)) {
                BitArray b = new BitArray(new int[] { style });
                Kiai = b[0];
                OmitFirstBarLine = b[3];
            } else throw new Exception("Failed to parse style of timing point: " + line);
        }

        public TimingPoint Copy() {
            return new TimingPoint(Offset, MpB, Meter, SampleSet, SampleIndex, Volume, Uninherited, Kiai, OmitFirstBarLine);
        }

        public bool ResnapSelf(Timing timing, IEnumerable<IBeatDivisor> beatDivisors, bool floor=true, TimingPoint tp=null, TimingPoint firstTp = null) {
            double newTime = timing.Resnap(Offset, beatDivisors, floor, tp: tp, firstTp: firstTp);
            double deltaTime = newTime - Offset;
            Offset += deltaTime;
            return deltaTime != 0;
        }

        public bool Equals(TimingPoint tp) {
            return Precision.AlmostEquals(Offset, tp.Offset) &&
                   Precision.AlmostEquals(MpB, tp.MpB) &&
                   Meter == tp.Meter &&
                   SampleSet == tp.SampleSet &&
                   SampleIndex == tp.SampleIndex &&
                   Precision.AlmostEquals(Volume, tp.Volume) &&
                   Uninherited == tp.Uninherited &&
                   Kiai == tp.Kiai &&
                   OmitFirstBarLine == tp.OmitFirstBarLine;
        }

        public bool SameEffect(TimingPoint tp) {
            if (tp.Uninherited && !Uninherited) {
                return Precision.AlmostEquals(MpB, -100) &&
                       Meter == tp.Meter &&
                       SampleSet == tp.SampleSet &&
                       SampleIndex == tp.SampleIndex &&
                       Precision.AlmostEquals(Volume, tp.Volume) &&
                       Kiai == tp.Kiai;
            }
            return Precision.AlmostEquals(MpB, tp.MpB) &&
                   Meter == tp.Meter &&
                   SampleSet == tp.SampleSet &&
                   SampleIndex == tp.SampleIndex &&
                   Precision.AlmostEquals(Volume, tp.Volume) &&
                   Kiai == tp.Kiai;
        }

        public double GetBpm() {
            if( Uninherited ) {
                return 60000 / MpB;
            }
            return -100 / MpB;
        }

        public void SetBpm(double bpm) {
            if (Uninherited) {
                MpB = 60000 / bpm;
            } else {
                MpB = -100 / bpm;
            }
        }

        public int CompareTo(TimingPoint other) {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var offsetComparison = Offset.CompareTo(other.Offset);
            if (offsetComparison != 0) return offsetComparison;
            return -Uninherited.CompareTo(other.Uninherited);
        }
    }
}