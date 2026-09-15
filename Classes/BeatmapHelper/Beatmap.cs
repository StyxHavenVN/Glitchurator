using StandalonePicturator.Classes.BeatmapHelper.Enums;
using StandalonePicturator.Classes.BeatmapHelper.Events;
using StandalonePicturator.Classes.MathUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StandalonePicturator.Classes.BeatmapHelper {

    public class Beatmap : ITextFile {
        public int Version { get; set; }
        public Dictionary<string, TValue> General { get; set; }
        public Dictionary<string, TValue> Editor { get; set; }
        public Dictionary<string, TValue> Metadata { get; set; }
        public Dictionary<string, TValue> Difficulty { get; set; }
        public List<ComboColour> ComboColours { get; set; }
        public Dictionary<string, ComboColour> SpecialColours { get; set; }
        public Timing BeatmapTiming { get; set; }
        public StoryBoard StoryBoard { get; set; }
        public List<Event> BackgroundAndVideoEvents => StoryBoard?.BackgroundAndVideoEvents ?? new List<Event>();
        public List<Break> BreakPeriods => StoryBoard?.BreakPeriods ?? new List<Break>();
        public List<Event> StoryboardLayerBackground => StoryBoard?.StoryboardLayerBackground ?? new List<Event>();
        public List<Event> StoryboardLayerFail => StoryBoard?.StoryboardLayerFail ?? new List<Event>();
        public List<Event> StoryboardLayerPass => StoryBoard?.StoryboardLayerPass ?? new List<Event>();
        public List<Event> StoryboardLayerForeground => StoryBoard?.StoryboardLayerForeground ?? new List<Event>();
        public List<Event> StoryboardLayerOverlay => StoryBoard?.StoryboardLayerOverlay ?? new List<Event>();
        public List<StoryboardSoundSample> StoryboardSoundSamples => StoryBoard?.StoryboardSoundSamples ?? new List<StoryboardSoundSample>();
        public List<HitObject> HitObjects { get; set; }
        public List<double> Bookmarks { get => GetBookmarks(); set => SetBookmarks(value); }
        public bool SaveWithFloatPrecision { get; set; }

        public Beatmap() {
            Initialize();
        }

        public Beatmap(List<HitObject> hitObjects, List<TimingPoint> timingPoints,
            TimingPoint firstUnInheritedTimingPoint = null, double globalSv = 1.4, GameMode gameMode = GameMode.Standard) {
            Initialize();

            HitObjects = hitObjects;
            BeatmapTiming.SetTimingPoints(timingPoints);
            BeatmapTiming.SliderMultiplier = globalSv;

            if (!BeatmapTiming.Contains(firstUnInheritedTimingPoint)) {
                BeatmapTiming.Add(firstUnInheritedTimingPoint);
            }

            Difficulty["SliderMultiplier"] = new TValue(globalSv.ToInvariant());
            General["Mode"] = new TValue(((int) gameMode).ToInvariant());

            SortHitObjects();
            CalculateSliderEndTimes();
            GiveObjectsGreenlines();
            CalculateHitObjectComboStuff();
        }

        public Beatmap(List<string> lines) {
            Initialize();
            SetLines(lines);
        }

        private void Initialize() {
            General = new Dictionary<string, TValue>();
            Editor = new Dictionary<string, TValue>();
            Metadata = new Dictionary<string, TValue>();
            Difficulty = new Dictionary<string, TValue>();
            ComboColours = new List<ComboColour>();
            SpecialColours = new Dictionary<string, ComboColour>();
            StoryBoard = new StoryBoard();
            HitObjects = new List<HitObject>();
            BeatmapTiming = new Timing(1.4);

            FillBasicMetadata();
        }

        public void FillBasicMetadata() {
            General["AudioFilename"] = new TValue(string.Empty);
            General["AudioLeadIn"] = new TValue("0");
            General["PreviewTime"] = new TValue("-1");
            General["Countdown"] = new TValue("0");
            General["SampleSet"] = new TValue("Soft");
            General["StackLeniency"] = new TValue("0.2");
            General["Mode"] = new TValue("0");
            General["LetterboxInBreaks"] = new TValue("0");
            General["WidescreenStoryboard"] = new TValue("0");

            Metadata["Title"] = new TValue(string.Empty);
            Metadata["TitleUnicode"] = new TValue(string.Empty);
            Metadata["Artist"] = new TValue(string.Empty);
            Metadata["ArtistUnicode"] = new TValue(string.Empty);
            Metadata["Creator"] = new TValue(string.Empty);
            Metadata["Version"] = new TValue(string.Empty);
            Metadata["Tags"] = new TValue(string.Empty);
            Metadata["BeatmapSetID"] = new TValue("-1");

            Difficulty["HPDrainRate"] = new TValue("5");
            Difficulty["CircleSize"] = new TValue("5");
            Difficulty["OverallDifficulty"] = new TValue("5");
            Difficulty["ApproachRate"] = new TValue("5");
            Difficulty["SliderMultiplier"] = new TValue("1.4");
            Difficulty["SliderTickRate"] = new TValue("1");
        }

        public void SetLines(List<string> lines) {
            Version = FileFormatHelper.TryParseInt(lines[0][17..].Trim(), out int version) ? version : 14;

            if (Version < 14) {
                Version = 14;
            }

            if (Version >= 128) {
                SaveWithFloatPrecision = true;
            }

            IEnumerable<string> generalLines = FileFormatHelper.GetCategoryLines(lines, "[General]");
            IEnumerable<string> editorLines = FileFormatHelper.GetCategoryLines(lines, "[Editor]");
            IEnumerable<string> metadataLines = FileFormatHelper.GetCategoryLines(lines, "[Metadata]");
            IEnumerable<string> difficultyLines = FileFormatHelper.GetCategoryLines(lines, "[Difficulty]");
            IEnumerable<string> timingLines = FileFormatHelper.GetCategoryLines(lines, "[TimingPoints]");
            IEnumerable<string> colourLines = FileFormatHelper.GetCategoryLines(lines, "[Colours]");
            IEnumerable<string> hitobjectLines = FileFormatHelper.GetCategoryLines(lines, "[HitObjects]");

            FileFormatHelper.FillDictionary(General, generalLines);
            FileFormatHelper.FillDictionary(Editor, editorLines);
            FileFormatHelper.FillDictionary(Metadata, metadataLines);
            FileFormatHelper.FillDictionary(Difficulty, difficultyLines);

            foreach (string line in colourLines) {
                try {
                    if (line.Substring(0, 5) == "Combo") {
                        ComboColours.Add(new ComboColour(line));
                    } else {
                        SpecialColours[FileFormatHelper.SplitKeyValue(line).Item1] = new ComboColour(line);
                    }
                } catch { }
            }

            foreach (string line in hitobjectLines) {
                try {
                    HitObjects.Add(new HitObject(line));
                } catch { }
            }

            // Bỏ qua lỗi parsing storyboard nếu beatmap chứa text README/ASCII lạ
            try {
                StoryBoard.SetLines(lines);
            } catch {
                StoryBoard = new StoryBoard();
            }

            BeatmapTiming = new Timing(timingLines, Difficulty["SliderMultiplier"].DoubleValue);

            SortHitObjects();
            CalculateHitObjectComboStuff();
            CalculateSliderEndTimes();
            GiveObjectsGreenlines();
        }

        public void SortHitObjects() {
            HitObjects.Sort();
        }

        public void CalculateSliderEndTimes() {
            foreach (var ho in HitObjects.Where(ho => ho.IsSlider)) {
                ho.CalculateSliderTemporalLength(BeatmapTiming, false);
            }
        }

        public void CalculateEndPositions() {
            foreach (var ho in HitObjects) {
                ho.CalculateEndPosition();
            }
        }

        internal void UpdateStacking(int startIndex = 0, int endIndex = -1, bool rounded = false) {
            if (endIndex == -1)
                endIndex = HitObjects.Count - 1;

            double stackOffset = GetStackOffset(Difficulty["CircleSize"].DoubleValue);
            double stackLeniency = General["StackLeniency"].DoubleValue;
            double preEmpt = GetApproachTime(Difficulty["ApproachRate"].DoubleValue);

            if (rounded) {
                stackOffset = Math.Round(stackOffset);
            }

            const int stackLenience = 3;

            Vector2 stackVector = new Vector2(stackOffset, stackOffset);
            float stackThresold = (float) (preEmpt * stackLeniency);

            for (int i = startIndex; i <= endIndex; i++)
                HitObjects[i].StackCount = 0;

            int extendedEndIndex = endIndex;
            for (int i = endIndex; i >= startIndex; i--) {
                int stackBaseIndex = i;
                for (int n = stackBaseIndex + 1; n < HitObjects.Count; n++) {
                    HitObject stackBaseObject = HitObjects[stackBaseIndex];
                    if (stackBaseObject.IsSpinner) break;

                    HitObject objectN = HitObjects[n];
                    if (objectN.IsSpinner) continue;

                    if (objectN.Time - stackBaseObject.EndTime > stackThresold)
                        break;

                    if (Vector2.Distance(stackBaseObject.Pos, objectN.Pos) < stackLenience ||
                        (stackBaseObject.IsSlider && Vector2.Distance(stackBaseObject.EndPos, objectN.Pos) < stackLenience)) {
                        stackBaseIndex = n;
                        objectN.StackCount = 0;
                    }
                }

                if (stackBaseIndex > extendedEndIndex) {
                    extendedEndIndex = stackBaseIndex;
                    if (extendedEndIndex == HitObjects.Count - 1)
                        break;
                }
            }

            int extendedStartIndex = startIndex;
            for (int i = extendedEndIndex; i > startIndex; i--) {
                int n = i;
                HitObject objectI = HitObjects[i];

                if (objectI.StackCount != 0 || objectI.IsSpinner) continue;

                if (objectI.IsCircle) {
                    while (--n >= 0) {
                        HitObject objectN = HitObjects[n];

                        if (objectN.IsSpinner) continue;

                        if (objectI.Time - objectN.EndTime > stackThresold)
                            break;

                        if (n < extendedStartIndex) {
                            objectN.StackCount = 0;
                            extendedStartIndex = n;
                        }

                        if (objectN.IsSlider && Vector2.Distance(objectN.EndPos, objectI.Pos) < stackLenience) {
                            int offset = objectI.StackCount - objectN.StackCount + 1;
                            for (int j = n + 1; j <= i; j++) {
                                if (Vector2.Distance(objectN.EndPos, HitObjects[j].Pos) < stackLenience)
                                    HitObjects[j].StackCount -= offset;
                            }
                            break;
                        }

                        if (Vector2.Distance(objectN.Pos, objectI.Pos) < stackLenience) {
                            objectN.StackCount = objectI.StackCount + 1;
                            objectI = objectN;
                        }
                    }
                } else if (objectI.IsSlider) {
                    while (--n >= startIndex) {
                        HitObject objectN = HitObjects[n];

                        if (objectN.IsSpinner) continue;

                        if (objectI.Time - objectN.Time > stackThresold)
                            break;

                        if (Vector2.Distance(objectN.EndPos, objectI.Pos) < stackLenience) {
                            objectN.StackCount = objectI.StackCount + 1;
                            objectI = objectN;
                        }
                    }
                }
            }

            for (int i = startIndex; i <= endIndex; i++) {
                HitObject currHitObject = HitObjects[i];
                currHitObject.StackedPos = currHitObject.Pos - currHitObject.StackCount * stackVector;
                currHitObject.StackedEndPos = currHitObject.EndPos - currHitObject.StackCount * stackVector;
            }
        }

        public void CalculateHitObjectComboStuff() {
            HitObject previousHitObject = null;
            int colourIndex = 0;
            int comboIndex = 0;

            var actingComboColours = ComboColours.Count == 0 ? ComboColour.GetDefaultComboColours() : ComboColours.ToArray();

            foreach (var hitObject in HitObjects) {
                hitObject.ActualNewCombo = IsNewCombo(hitObject, previousHitObject);

                if (hitObject.ActualNewCombo) {
                    var colourIncrement = hitObject.IsSpinner ? hitObject.ComboSkip : hitObject.ComboSkip + 1;

                    colourIndex = MathHelper.Mod(colourIndex + colourIncrement, actingComboColours.Length);
                    comboIndex = 1;
                } else {
                    comboIndex++;
                }

                hitObject.ComboIndex = comboIndex;
                hitObject.ColourIndex = colourIndex;
                hitObject.Colour = actingComboColours[colourIndex];

                previousHitObject = hitObject;
            }
        }

        public void FixComboSkip() {
            HitObject previousHitObject = null;
            int colourIndex = 0;

            var actingComboColours = ComboColours.Count == 0 ? ComboColour.GetDefaultComboColours() : ComboColours.ToArray();

            foreach (var hitObject in HitObjects) {
                bool newCombo = IsNewCombo(hitObject, previousHitObject);

                if (newCombo) {
                    int colourIncrement = hitObject.IsSpinner ? 0 : 1;
                    var newColourIndex = MathHelper.Mod(colourIndex + colourIncrement, actingComboColours.Length);
                    var wantedColourIndex = hitObject.ColourIndex;
                    var diff = wantedColourIndex - newColourIndex;

                    if (diff > 0) {
                        hitObject.ComboSkip = diff;
                    } else if (diff < 0) {
                        hitObject.ComboSkip = (actingComboColours.Length + diff);
                    }

                    int newColourIncrement = hitObject.IsSpinner ? hitObject.ComboSkip : hitObject.ComboSkip + 1;
                    colourIndex = MathHelper.Mod(colourIndex + newColourIncrement, actingComboColours.Length);
                }

                previousHitObject = hitObject;
            }
        }

        public static bool IsNewCombo(HitObject hitObject, HitObject previousHitObject) {
            return hitObject.NewCombo || hitObject.IsSpinner || previousHitObject == null || previousHitObject.IsSpinner;
        }

        public void GiveObjectsGreenlines() {
            foreach (var ho in HitObjects) {
                ho.SliderVelocity = BeatmapTiming.GetSvAtTime(ho.Time);
                ho.TimingPoint = BeatmapTiming.GetTimingPointAtTime(ho.Time);
                ho.HitsoundTimingPoint = BeatmapTiming.GetTimingPointAtTime(ho.Time + 5);
                ho.UnInheritedTimingPoint = BeatmapTiming.GetRedlineAtTime(ho.Time);
                ho.BodyHitsounds = BeatmapTiming.GetTimingPointsInRange(ho.Time, ho.EndTime, false);
                foreach (var time in ho.GetAllTloTimes(BeatmapTiming)) {
                    ho.BodyHitsounds.RemoveAll(o => Math.Abs(time - o.Offset) <= 5);
                }
            }
        }

        public static double GetApproachTime(double approachRate) {
            if (approachRate < 5) {
                return 1800 - 120 * approachRate;
            }

            return 1200 - 150 * (approachRate - 5);
        }

        public static double GetHitObjectRadius(double circleSize) {
            return (109 - 9 * circleSize) / 2;
        }

        public static double GetStackOffset(double circleSize) {
            return GetHitObjectRadius(circleSize) / 10;
        }

        public List<HitObject> GetHitObjectsWithRangeInRange(double start, double end) {
            return HitObjects.FindAll(o => o.EndTime >= start && o.Time <= end);
        }

        public Timeline GetTimeline() {
            Timeline tl = new Timeline(HitObjects, BeatmapTiming);
            tl.GiveTimingPoints(BeatmapTiming);
            return tl;
        }

        public List<double> GetBookmarks() {
            try {
                return Editor["Bookmarks"].GetDoubleList();
            }
            catch (KeyNotFoundException) {
                return new List<double>();
            }
        }

        public void SetBookmarks(List<double> bookmarks) {
            if (bookmarks.Count > 0) {
                Editor["Bookmarks"] = new TValue(string.Join(",", bookmarks.Select(d => Math.Round(d))));
            }
        }

        public List<HitObject> GetBookmarkedObjects(double leniency = 5) {
            List<double> bookmarks = GetBookmarks();
            List<HitObject> markedObjects = HitObjects.FindAll(ho =>
                bookmarks.Exists(o => ho.Time - leniency <= o && o <= ho.EndTime + leniency));
            return markedObjects;
        }

        public List<string> GetLines() {
            List<string> lines = new List<string>
            {
                "osu file format v" + Version.ToInvariant(),
                "",
                "[General]"
            };
            FileFormatHelper.AddDictionaryToLines(General, lines, spaceBeforeValue: true);
            lines.Add("");
            lines.Add("[Editor]");
            FileFormatHelper.AddDictionaryToLines(Editor, lines, spaceBeforeValue: true);
            lines.Add("");
            lines.Add("[Metadata]");
            FileFormatHelper.AddDictionaryToLines(Metadata, lines, spaceBeforeValue: Version >= 128);
            lines.Add("");
            lines.Add("[Difficulty]");
            FileFormatHelper.AddDictionaryToLines(Difficulty, lines, spaceBeforeValue: Version >= 128);
            lines.Add("");
            lines.Add("[Events]");
            if (Version < 128)
                lines.Add("//Background and Video events");
            lines.AddRange(BackgroundAndVideoEvents.Select(e => {
                e.SaveWithFloatPrecision = SaveWithFloatPrecision;
                return e.GetLine();
            }));
            if (Version < 128)
                lines.Add("//Break Periods");
            lines.AddRange(BreakPeriods.Select(b => {
                b.SaveWithFloatPrecision = SaveWithFloatPrecision;
                return b.GetLine();
            }));
            if (Version < 128)  
                lines.Add("//Storyboard Layer 0 (Background)");
            lines.AddRange(Event.SerializeEventTree(StoryboardLayerBackground, saveWithFloatPrecision: SaveWithFloatPrecision));
            if (Version < 128)
                lines.Add("//Storyboard Layer 1 (Fail)");
            lines.AddRange(Event.SerializeEventTree(StoryboardLayerFail, saveWithFloatPrecision: SaveWithFloatPrecision));
            if (Version < 128)
                lines.Add("//Storyboard Layer 2 (Pass)");
            lines.AddRange(Event.SerializeEventTree(StoryboardLayerPass, saveWithFloatPrecision: SaveWithFloatPrecision));
            if (Version < 128)
                lines.Add("//Storyboard Layer 3 (Foreground)");
            lines.AddRange(Event.SerializeEventTree(StoryboardLayerForeground, saveWithFloatPrecision: SaveWithFloatPrecision));
            if (Version < 128)
                lines.Add("//Storyboard Layer 4 (Overlay)");
            lines.AddRange(Event.SerializeEventTree(StoryboardLayerOverlay, saveWithFloatPrecision: SaveWithFloatPrecision));
            if (Version < 128)
                lines.Add("//Storyboard Sound Samples");
            lines.AddRange(StoryboardSoundSamples.Select(sbss => {
                sbss.SaveWithFloatPrecision = SaveWithFloatPrecision;
                return sbss.GetLine();
            }));
            lines.Add("");
            lines.Add("[TimingPoints]");
            lines.AddRange(BeatmapTiming.TimingPoints.Where(tp => tp != null).Select(tp => {
                tp.SaveWithFloatPrecision = SaveWithFloatPrecision;
                return tp.GetLine();
            }));
            if (Version < 128)
                lines.Add("");
            if (ComboColours.Any()) {
                lines.Add("");
                lines.Add("[Colours]");
                lines.AddRange(ComboColours.Select((t, i) => "Combo" + (i + 1) + (Version < 128 ? " : " : ": ") + t));
                lines.AddRange(SpecialColours.Select(specialColour => specialColour.Key + (Version < 128 ? " : " : ": ") + specialColour.Value));
            }
            lines.Add("");
            lines.Add("[HitObjects]");
            lines.AddRange(HitObjects.Select(ho => {
                ho.SaveWithFloatPrecision = SaveWithFloatPrecision;
                return ho.GetLine();
            }));
            lines.Add("");

            return lines;
        }

        public double GetHitObjectStartTime() {
            return HitObjects.Count > 0 ? HitObjects.Min(h => h.Time) : 0;
        }

        public double GetHitObjectEndTime() {
            return HitObjects.Count > 0 ? HitObjects.Max(h => h.EndTime) : 0;
        }

        public void OffsetTime(double offset) {
            BeatmapTiming.Offset(offset);
            HitObjects?.ForEach(h => h.MoveTime(offset));
        }

        public string GetFileName() {
            return GetFileName(Metadata["Artist"].Value, Metadata["Title"].Value,
                Metadata["Creator"].Value, Metadata["Version"].Value, Version);
        }

        public static string GetFileName(string artist, string title, string creator, string version, int formatVersion = 14) {
            string fileName = $"{artist} - {title} ({creator}) [{version}]";

            string regexSearch = new string(Path.GetInvalidFileNameChars());
            Regex r = new Regex($"[{Regex.Escape(regexSearch)}]");
            string replacement = formatVersion < 128 ? "" : "_";  
            fileName = r.Replace(fileName, replacement);

            return fileName.Substring(0, Math.Min(184, fileName.Length)) + ".osu";
        }

        public Beatmap DeepCopy() {
            var newBeatmap = (Beatmap)MemberwiseClone();
            newBeatmap.HitObjects = HitObjects?.Select(h => h.DeepCopy()).ToList();
            newBeatmap.BeatmapTiming = new Timing(BeatmapTiming.TimingPoints.Select(t => t.Copy()).ToList(), BeatmapTiming.SliderMultiplier);
            newBeatmap.GiveObjectsGreenlines();
            return newBeatmap;
        }
    }
}