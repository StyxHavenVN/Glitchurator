using System;
using System.Linq;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.SliderPathStuff;
using StandalonePicturator.Classes.MathUtil;

namespace StandalonePicturator.Classes;

// Manages multi-route sliderball multiplexing (rapid switching between slider paths for multi-ball visual illusion)
public sealed class MultiplexBallMotion
{
    private readonly SliderPath[] paths;
    private readonly BallGraphPoint[][] graphs;

    public int Count => paths.Length;
    public double SwitchMilliseconds { get; }
    public double Duration { get; }

    public MultiplexBallMotion(HitObject[] sliders, BallGraphPoint[][] graphs, double duration, double switchMilliseconds)
    {
        if (sliders.Length == 0 || graphs.Length != sliders.Length)
        {
            throw new ArgumentException("At least one matching path and graph is required.");
        }

        // Cache isolated slider paths and their corresponding motion curves
        paths = sliders.Select(s => s.DeepCopy().GetSliderPath()).ToArray();
        this.graphs = graphs.Select(g => g?.ToArray()).ToArray();

        Duration = Math.Max(2, duration);
        SwitchMilliseconds = Math.Clamp(switchMilliseconds, 0.1, 32.0);
    }

    // Determines active route index based on high-frequency time-sliced intervals
    public int RouteAt(double progress)
    {
        double currentMs = Math.Clamp(progress, 0, 1) * Duration + 1e-7;
        return (int)(Math.Floor(currentMs / SwitchMilliseconds) % Count);
    }

    // Evaluates 2D coordinates on a specific route using its motion graph
    public Vector2 PositionOnRoute(int index, double progress)
    {
        double t = Math.Clamp(progress, 0, 1);
        double routeProgress = graphs[index] == null 
            ? t 
            : BallMotionGraph.Evaluate(graphs[index], t);

        return paths[index].PositionAt(routeProgress);
    }

    // Resolves current multiplexed ball position at normalized progress [0, 1]
    public Vector2 PositionAt(double progress) => PositionOnRoute(RouteAt(progress), progress);
}