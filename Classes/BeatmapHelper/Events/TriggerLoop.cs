using System.Text;
using static StandalonePicturator.Classes.BeatmapHelper.FileFormatHelper;

namespace StandalonePicturator.Classes.BeatmapHelper.Events {
    /// <summary>
    /// Represents trigger loop events. Although called loops, these only ever activate once.
    /// </summary>
    public class TriggerLoop : Command, IHasEndTime {
        public override EventType EventType => EventType.T;
        public double EndTime { get; set; }
        public string TriggerName { get; set; }

        public override string GetLine() {
            return $"{EventType},{TriggerName},{(SaveWithFloatPrecision ? StartTime.ToInvariant() : StartTime.ToRoundInvariant())},{(SaveWithFloatPrecision ? EndTime.ToInvariant() : EndTime.ToRoundInvariant())}";
        }

        public override void SetLine(string line) {
            var subLine = RemoveIndents(line);
            var values = subLine.Split(',');

            TriggerName = values[1];

            if (TryParseDouble(values[2], out double startTime))
                StartTime = startTime;
            else throw new BeatmapParsingException("Failed to parse start time of event param.", line);

            if (TryParseDouble(values[3], out double endTime))
                EndTime = endTime;
            else throw new BeatmapParsingException("Failed to parse end time of event param.", line);
        }
    }
}