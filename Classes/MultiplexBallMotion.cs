using System;
using System.Linq;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.SliderPathStuff;
using StandalonePicturator.Classes.MathUtil;

namespace StandalonePicturator.Classes;

// Immutable motion snapshot: each route advances throughout the shared duration.
public sealed class MultiplexBallMotion
{
    private readonly SliderPath[] paths;
    private readonly BallGraphPoint[][] graphs;
    public int Count => paths.Length;
    public int SwitchMilliseconds { get; }
    public double Duration { get; }
    public MultiplexBallMotion(HitObject[] sliders, BallGraphPoint[][] graphs, double duration, int switchMilliseconds)
    {
        if (sliders.Length == 0 || graphs.Length != sliders.Length) throw new ArgumentException("At least one matching path and graph is required.");
        paths = sliders.Select(s => s.DeepCopy().GetSliderPath()).ToArray();
        this.graphs = graphs.Select(g => g?.ToArray()).ToArray();
        Duration = Math.Max(2, duration);
        SwitchMilliseconds = Math.Clamp(switchMilliseconds, 1, 32);
    }
    public int RouteAt(double progress) => (int)(Math.Floor((Math.Clamp(progress, 0, 1) * Duration + 1e-7) / SwitchMilliseconds) % Count);
    public Vector2 PositionOnRoute(int index, double progress) => paths[index].PositionAt(graphs[index] == null
        ? Math.Clamp(progress, 0, 1) : BallMotionGraph.Evaluate(graphs[index], Math.Clamp(progress, 0, 1)));
    public Vector2 PositionAt(double progress) => PositionOnRoute(RouteAt(progress), progress);
}
