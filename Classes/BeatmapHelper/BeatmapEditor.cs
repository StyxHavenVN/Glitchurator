using System.Collections.Generic;
using System.IO;

namespace StandalonePicturator.Classes.BeatmapHelper {
    public class BeatmapEditor : Editor
    {
        public Beatmap Beatmap => (Beatmap)TextFile;

        public BeatmapEditor(List<string> lines)
        {
            TextFile = new Beatmap(lines);
        }

        public BeatmapEditor(string path)
        {
            Path = path;
            TextFile = new Beatmap(ReadFile(Path));
        }

        public void SaveFileWithNameUpdate() {
            File.Delete(Path);
            Path = System.IO.Path.Combine(GetParentFolder(), Beatmap.GetFileName());
            SaveFile();
        }

        public override void SaveFile() {
            base.SaveFile();
        }

        public override void SaveFile(string path) {
            base.SaveFile(path);
        }

        public override void SaveFile(List<string> lines) {
            base.SaveFile(lines);
        }
    }
}