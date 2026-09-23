using System.Linq;
using StandalonePicturator.Classes;

namespace StandalonePicturator.Viewmodel;

public partial class SliderPicturatorVm
{
    private double ballSwitchMilliseconds = 4;
    public double BallSwitchMilliseconds {
        get => ballSwitchMilliseconds;
        set { if (Set(ref ballSwitchMilliseconds, System.Math.Clamp(double.IsFinite(value) ? value : 4, .1, 32))) SaveSession(); }
    }
    public MultiplexBallMotion CreateMultiplexMotion(double minimumInterval = 1)
    {
        if (!HasSliderBall || !ChainAllVisibleBallPaths) return null;
        var sources = VisibleLayers.Where(l => l.HasSliderBall)
            .Select(l => (Layer: l, Slider: l.CreateSingleBallSlider())).Where(s => s.Slider != null).ToArray();
        if (sources.Length == 0) return null;
        return new MultiplexBallMotion(sources.Select(s => s.Slider).ToArray(),
            sources.Select(s => s.Layer.BallGraphEnabled ? s.Layer.BallGraphPoints.ToArray() : null).ToArray(),
            Duration, System.Math.Max(minimumInterval, BallSwitchMilliseconds));
    }
}
