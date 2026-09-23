using System;
using System.Collections.Generic;
using System.Linq;
using StandalonePicturator.Classes;

namespace StandalonePicturator.Viewmodel;

public partial class SliderPicturatorVm
{
    private LayerEffects layerEffects = new();
    private void EffectChanged(string name) { RaisePropertyChanged(name); if (!loadingSession) { RefreshSource(); SaveSession(); } }

    public bool LayeredGlitch 
    { 
        get => layerEffects.Layered; 
        set 
        { 
            layerEffects.Layered = value; 
            RaisePropertyChanged(nameof(IsAnyGlitchEnabled));
            EffectChanged(nameof(LayeredGlitch)); 
        } 
    }
    public bool RandomGlitchRows { get => layerEffects.Random; set { layerEffects.Random = value; EffectChanged(nameof(RandomGlitchRows)); } }

    // Direction chỉ lưu góc xoay độc lập cho thuật toán Glitch, không can thiệp bật LayeredGlitch
    public double GlitchAngle 
    { 
        get => GlitchRotationTarget == "Body" ? layerEffects.BodyAngle ?? layerEffects.Angle : GlitchRotationTarget == "Border" ? layerEffects.BorderAngle ?? layerEffects.Angle : layerEffects.Angle; 
        set 
        { 
            if (!double.IsFinite(value)) return; 
            layerEffects.BodyAngle ??= layerEffects.Angle;
            layerEffects.BorderAngle ??= layerEffects.Angle;
            double angle = Math.Clamp(value, -180, 180);
            if (GlitchRotationTarget != "Border") layerEffects.BodyAngle = angle;
            if (GlitchRotationTarget != "Body") layerEffects.BorderAngle = angle;
            layerEffects.Angle = angle; 
            EffectChanged(nameof(GlitchAngle)); 
        } 
    }

    public string[] GlitchRotationTargets => new[] { "Both", "Body", "Border" };
    public string GlitchRotationTarget {
        get => layerEffects.RotationTarget;
        set {
            if (!GlitchRotationTargets.Contains(value)) return;
            layerEffects.RotationTarget = value;
            RaisePropertyChanged(nameof(GlitchRotationTarget));
            RaisePropertyChanged(nameof(GlitchAngle));
            if (!loadingSession) SaveSession();
        }
    }

    public double GlitchSpacing { get => layerEffects.Spacing; set { if (!double.IsFinite(value)) return; layerEffects.Spacing = Math.Clamp(value, 1, 30); EffectChanged(nameof(GlitchSpacing)); } }
    public int GlitchTiers { get => layerEffects.Tiers; set { layerEffects.Tiers = Math.Clamp(value, 2, 10); EffectChanged(nameof(GlitchTiers)); } }
    public double GlitchOuterDensity { get => layerEffects.OuterDensity; set { if (!double.IsFinite(value)) return; layerEffects.OuterDensity = Math.Clamp(value, 0, 100); EffectChanged(nameof(GlitchOuterDensity)); } }
    public double GlitchMiddleDensity { get => layerEffects.MiddleDensity; set { if (!double.IsFinite(value)) return; layerEffects.MiddleDensity = Math.Clamp(value, 0, 100); EffectChanged(nameof(GlitchMiddleDensity)); } }
    public double GlitchSolidCore { get => layerEffects.Core; set { if (!double.IsFinite(value)) return; layerEffects.Core = Math.Clamp(value, 0, 95); EffectChanged(nameof(GlitchSolidCore)); } }
    public int CutCount => layerEffects.Cuts.Count;
    public void AddShapeCut(ShapeCut cut) { layerEffects.Cuts.Add(cut.Copy()); EffectChanged(nameof(CutCount)); }
    public void UndoShapeCut() { if (layerEffects.Cuts.Count == 0) return; layerEffects.Cuts.RemoveAt(layerEffects.Cuts.Count - 1); EffectChanged(nameof(CutCount)); }
    public void ClearShapeCuts() { layerEffects.Cuts.Clear(); EffectChanged(nameof(CutCount)); }
    private void NotifyEffects() { foreach (var name in new[] { nameof(GlitchRotationTarget), nameof(LayeredGlitch), nameof(RandomGlitchRows), nameof(GlitchAngle), nameof(GlitchSpacing), nameof(GlitchTiers), nameof(GlitchOuterDensity), nameof(GlitchMiddleDensity), nameof(GlitchSolidCore), nameof(CutCount) }) RaisePropertyChanged(name); }
}
