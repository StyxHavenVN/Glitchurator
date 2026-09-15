using System;
using System.Collections.Generic;
using StandalonePicturator.Classes.BeatmapHelper;
using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.MathUtil;

namespace StandalonePicturator.Classes.Tools
{
    public static class NegativeSpaceSlider
    {
        public static HitObject GenerateHollowDonut(Vector2 center, double outerRadius, double innerRadius, double time, int segments = 32)
        {
            return GenerateHollowPolygon(center, outerRadius, innerRadius, sides: segments, time: time, rotationDegrees: 0);
        }

        public static HitObject GenerateHollowPolygon(Vector2 center, double outerRadius, double innerRadius, int sides, double time, double rotationDegrees = 0)
        {
            if (sides < 3) sides = 3;
            if (innerRadius >= outerRadius) innerRadius = outerRadius * 0.5;

            List<Vector2> anchors = new List<Vector2>();
            double rotRad = rotationDegrees * Math.PI / 180.0;

            // 1. Quét vòng ngoài (CW)
            for (int i = 0; i <= sides; i++)
            {
                double a = rotRad + (2.0 * Math.PI * i) / sides;
                anchors.Add(new Vector2(
                    Math.Round(center.X + outerRadius * Math.Cos(a), 1),
                    Math.Round(center.Y + outerRadius * Math.Sin(a), 1)
                ));
            }

            Vector2 seamOuter = anchors[anchors.Count - 1];
            Vector2 seamInner = new Vector2(
                Math.Round(center.X + innerRadius * Math.Cos(rotRad), 1),
                Math.Round(center.Y + innerRadius * Math.Sin(rotRad), 1)
            );

            // 2. Đi vào vành trong (Invisible Seam In)
            anchors.Add(seamInner);

            // 3. Quét vòng trong (CCW) - Bắt đầu từ 1 để tránh trùng điểm seamInner
            for (int i = 1; i <= sides; i++)
            {
                double a = rotRad + (2.0 * Math.PI * (sides - i)) / sides;
                anchors.Add(new Vector2(
                    Math.Round(center.X + innerRadius * Math.Cos(a), 1),
                    Math.Round(center.Y + innerRadius * Math.Sin(a), 1)
                ));
            }

            // 4. Đi ngược lại ra vành ngoài (Invisible Seam Out)
            anchors.Add(seamOuter);

            // KHỞI TẠO VÀ ÉP TYPE = SLIDER (TRIỆT TIÊU CỜ CIRCLE)
            var ho = new HitObject(time, 0, SampleSet.None, SampleSet.None)
            {
                IsCircle = false,      // BẮT BUỘC: Không để Type bị cộng 1 (Circle)
                IsSpinner = false,
                IsHoldNote = false,
                IsSlider = true,       // Type = 2
                SliderType = PathType.Linear,
                Repeat = 1
            };

            ho.SetAllCurvePoints(anchors);

            double len = 0;
            for (int i = 1; i < anchors.Count; i++) len += (anchors[i] - anchors[i - 1]).Length;
            ho.PixelLength = len;

            return ho;
        }
    }
}