using System;
using System.Collections.Generic;
using System.Linq;
using StandalonePicturator.Classes;

namespace StandalonePicturator.Viewmodel;

public partial class SliderPicturatorVm
{
    private double minimumTumourLength = 12;
    public double MinimumTumourLength {
        get => minimumTumourLength;
        set { if (double.IsFinite(value) && Set(ref minimumTumourLength, Math.Clamp(value, 1, 12))) SaveSession(); }
    }
    private bool ballGraphEnabled;
    private IReadOnlyList<BallGraphPoint> ballGraphPoints = Array.AsReadOnly(BallMotionGraph.Default());

    public bool BallGraphEnabled
    {
        get => ballGraphEnabled;
        set { if (Set(ref ballGraphEnabled, value)) { if (value) HasSliderBall = true; SaveSession(); } }
    }

    public IReadOnlyList<BallGraphPoint> BallGraphPoints => ballGraphPoints;

    public void ReplaceBallGraph(IEnumerable<BallGraphPoint> points)
    {
        ballGraphPoints = Array.AsReadOnly(BallMotionGraph.Normalize(points));
        RaisePropertyChanged(nameof(BallGraphPoints));
        BallGraphEnabled = true;
        SaveSession();
    }

    public void EditBallGraphPoint(int index, double time, double position, BallGraphCurve curve, double curvature = 0)
    {
        if (index < 0 || index >= ballGraphPoints.Count || !double.IsFinite(time) || !double.IsFinite(position)) return;
        var points = ballGraphPoints.ToArray();
        time = index == 0 ? 0 : index == points.Length - 1 ? 1
            : Math.Clamp(time, points[index - 1].Time, points[index + 1].Time);
        points[index] = new BallGraphPoint(time, Math.Clamp(position, 0, 1), curve, Math.Clamp(curvature, -1, 1));
        ReplaceBallGraph(points);
    }

    // CẬP NHẬT TỌA ĐỘ TRỰC TIẾP TRÊN BỘ NHỚ KHI ĐANG KÉO (0ms DELAY)
    public void UpdatePointLive(int index, double time, double position)
    {
        if (index < 0 || index >= ballGraphPoints.Count) return;
        var points = ballGraphPoints.ToArray();
        time = index == 0 ? 0 : index == points.Length - 1 ? 1
            : Math.Clamp(time, points[index - 1].Time, points[index + 1].Time);
        var cur = points[index];
        points[index] = new BallGraphPoint(time, Math.Clamp(position, 0, 1), cur.Curve, cur.Curvature);
        ballGraphPoints = Array.AsReadOnly(points);
        RaisePropertyChanged(nameof(BallGraphPoints));
    }

    // CẬP NHẬT ĐỘ CONG / SÓNG TRỰC TIẾP TRÊN BỘ NHỚ KHI KÉO NỐT NHỎ
    public void UpdateCurvatureLive(int index, double curvature)
    {
        if (index < 0 || index >= ballGraphPoints.Count) return;
        var points = ballGraphPoints.ToArray();
        var cur = points[index];
        points[index] = new BallGraphPoint(cur.Time, cur.Position, cur.Curve, Math.Clamp(curvature, -1, 1));
        ballGraphPoints = Array.AsReadOnly(points);
        RaisePropertyChanged(nameof(BallGraphPoints));
    }

    public void CommitGraphChanges()
    {
        ballGraphPoints = Array.AsReadOnly(BallMotionGraph.Normalize(ballGraphPoints));
        RaisePropertyChanged(nameof(BallGraphPoints));
        BallGraphEnabled = true;
        SaveSession(); // Chỉ lưu ổ cứng khi nhả chuột
    }

    public double EvaluateBallProgress(double time) => BallGraphEnabled
        ? BallMotionGraph.Evaluate(ballGraphPoints, time) : Math.Clamp(time, 0, 1);

    private double previewProgress;
    public event Action<double, bool> PreviewProgressChanged;

    public double GetPreviewProgress() => previewProgress;

    public void SetPreviewProgress(double value, bool seek = false)
    {
        if (!double.IsFinite(value)) return;
        value = Math.Clamp(value, 0, 1);
        if (value == previewProgress && !seek) return;
        previewProgress = value;
        PreviewProgressChanged?.Invoke(value, seek);
    }
}