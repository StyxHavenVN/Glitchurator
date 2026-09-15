using System;

namespace StandalonePicturator.Classes.BeatmapHelper.BeatDivisors {
    public interface IBeatDivisor : IEquatable<IBeatDivisor> {
        double GetValue();
    }
}