namespace StandalonePicturator.Classes.BeatmapHelper.Events {
    /// <summary>
    /// Indicates that a type has an end time. Used by Property Transformer on Events
    /// </summary>
    public interface IHasEndTime {
        double EndTime { get; set; }
    }
}