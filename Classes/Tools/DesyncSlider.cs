using System;
using System.Collections.Generic;
using System.Linq;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;

namespace StandalonePicturator.Classes.Tools
{
    public static class DesyncSlider
    {
        /// <summary>
        /// Tạo duy nhất 1 HitObject Quantum Slider: Thân slider hiển thị một nơi,
        /// nhưng Hitbox và Quả bóng bị bẻ gãy sang một tọa độ hoàn toàn khác.
        /// </summary>
        public static HitObject CreateSingleQuantumDesync(
            HitObject visualSource, 
            Vector2 targetHitbox, 
            double time, 
            double duration)
        {
            var rawPoints = visualSource.GetAllCurvePoints();
            if (rawPoints == null || rawPoints.Count < 2)
            {
                throw new Exception("Invalid source slider.");
            }

            var anchors = new List<Vector2>(rawPoints);
            Vector2 vk = anchors[anchors.Count - 1];

            // 1. Tính toán vector tiếp tuyến dẫn thẳng từ điểm cuối thân slider đến vị trí Hitbox
            Vector2 direction = targetHitbox - vk;
            double distanceToTarget = direction.Length;

            // Nhồi vector định hướng cực vi mô (0.001px) để CPU bẻ hướng ngoại suy
            if (distanceToTarget > 0.001)
            {
                Vector2 microTangent = vk + (direction / distanceToTarget) * 0.001;
                anchors.Add(new Vector2(Math.Round(microTangent.X, 3), Math.Round(microTangent.Y, 3)));
            }
            else
            {
                anchors.Add(vk + new Vector2(0.001, 0));
                distanceToTarget = 0.001;
            }

            // 2. Tính chiều dài hình học thực tế của thân slider
            double geomLength = 0;
            for (int i = 1; i < anchors.Count; i++)
            {
                geomLength += (anchors[i] - anchors[i - 1]).Length;
            }

            // 3. Ép PixelLength vượt ngưỡng hình học để kích hoạt CPU Extrapolation
            // GPU chỉ vẽ tới geomLength (thân visual), phần dôi ra (distanceToTarget) CPU sẽ đẩy bóng tới targetHitbox
            double quantumPixelLength = geomLength + distanceToTarget;

            var quantumSlider = new HitObject(time, 0, SampleSet.None, SampleSet.None)
            {
                IsCircle = false,
                IsSpinner = false,
                IsHoldNote = false,
                IsSlider = true,
                SliderType = PathType.Linear,
                Repeat = 1,
                PixelLength = Math.Round(quantumPixelLength, 3)
            };

            quantumSlider.SetAllCurvePoints(anchors);
            return quantumSlider;
        }
    }
}