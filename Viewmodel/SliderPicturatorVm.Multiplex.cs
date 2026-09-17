using System.Linq;
using StandalonePicturator.Classes;

namespace StandalonePicturator.Viewmodel;

public partial class SliderPicturatorVm
{
    private int ballSwitchMilliseconds = 4;
    public int BallSwitchMilliseconds {
        get => ballSwitchMilliseconds;
        set { if (Set(ref ballSwitchMilliseconds, System.Math.Clamp(value, 1, 32))) SaveSession(); }
    }
    public MultiplexBallMotion CreateMultiplexMotion()
    {
        if (!HasSliderBall || !ChainAllVisibleBallPaths) return null;
        var sources = VisibleLayers.Where(l => l.HasSliderBall)
            .Select(l => (Layer: l, Slider: l.CreateSingleBallSlider())).Where(s => s.Slider != null).ToArray();
        if (sources.Length == 0) return null;
        return new MultiplexBallMotion(sources.Select(s => s.Slider).ToArray(),
            sources.Select(s => s.Layer.BallGraphEnabled ? s.Layer.BallGraphPoints.ToArray() : null).ToArray(),
            Duration, BallSwitchMilliseconds);
    }
}
