using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using StandalonePicturator.Classes;
using StandalonePicturator.Classes.BeatmapHelper;

namespace StandalonePicturator.Viewmodel;

public sealed class PicturatorLibraryItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    private string name;
    public string Name { get => name; set { name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    public string Kind { get; set; }
    private bool isVisible = true;
    public bool IsVisible { get => isVisible; set { isVisible = value; PropertyChanged?.Invoke(this, new(nameof(IsVisible))); } }
    public string State { get; set; }
    public event PropertyChangedEventHandler PropertyChanged;
}

public partial class SliderPicturatorVm
{
    [JsonIgnore] public ObservableCollection<PicturatorLibraryItem> LibraryItems { get; } = new();
    private bool switchingLibrary, libraryReady;
    private readonly Dictionary<string, (string State, SliderPicturatorVm Model)> layerCache = new();
    [JsonIgnore] public IEnumerable<SliderPicturatorVm> VisibleLayers
    {
        get {
            if (LibraryItems.Count == 0) { if (Bm != null) yield return this; yield break; }
            foreach (var item in LibraryItems.Where(i => i.IsVisible)) {
                if (item == activeLibraryItem) { if (Bm != null) yield return this; continue; }
                if (!layerCache.TryGetValue(item.Id, out var cached) || cached.State != item.State) {
                    if (cached.Model != null) { cached.Model.previewTokenSource?.Cancel(); cached.Model.Bm = null; }
                    var layer = new SliderPicturatorVm(true);
                    layer.RestoreSessionData(JsonConvert.DeserializeObject<SessionData>(item.State));
                    layer.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(BmImage)) RaisePropertyChanged(nameof(VisibleLayers)); };
                    cached = (item.State, layer); layerCache[item.Id] = cached;
                }
                if (cached.Model.Bm != null) yield return cached.Model;
            }
        }
    }
    private PicturatorLibraryItem activeLibraryItem;
    private static string LibraryFile => PicturatorStorage.FilePath("picturator_library.json");
    [JsonIgnore] public PicturatorLibraryItem ActiveLibraryItem {
        get => activeLibraryItem;
        set {
            if (value == activeLibraryItem || value == null || !LibraryItems.Contains(value)) return;
            var state = JsonConvert.DeserializeObject<SessionData>(value.State);
            if (state == null) return;
            state.BeatmapPath = BeatmapPath;
            SaveLibrary(); switchingLibrary = true;
            try { activeLibraryItem = value; RestoreSessionData(state); }
            finally { switchingLibrary = false; }
            RaisePropertyChanged(nameof(ActiveLibraryItem)); SaveSession();
        }
    }
    private string libraryStatus = "Copy sliders in osu! editor with Ctrl+C, then click Load copied sliders.";
    [JsonIgnore] public string LibraryStatus { get => libraryStatus; private set => Set(ref libraryStatus, value); }
    private sealed class LibraryData { public List<PicturatorLibraryItem> Items { get; set; } = new(); public string ActiveId { get; set; } }

    // Restore saved layers and the active selection; each layer owns its independent settings.
    private void InitializeLibrary()
    {
        switchingLibrary = true;
        try {
            if (File.Exists(LibraryFile)) {
                var saved = JsonConvert.DeserializeObject<LibraryData>(File.ReadAllText(LibraryFile));
                foreach (var item in saved?.Items ?? new()) {
                    if (JsonConvert.DeserializeObject<SessionData>(item.State ?? "null") == null) continue;
                    LibraryItems.Add(item); item.PropertyChanged += (_, _) => SaveLibrary();
                }
                activeLibraryItem = LibraryItems.FirstOrDefault(i => i.Id == saved?.ActiveId) ?? LibraryItems.FirstOrDefault();
                if (activeLibraryItem != null) RestoreSessionData(JsonConvert.DeserializeObject<SessionData>(activeLibraryItem.State));
            }
            if (LibraryItems.Count == 0 && Bm != null) {
                var item = new PicturatorLibraryItem { Name = SelectedSlider != null ? $"Slider • {TimeCode:0} ms" : System.IO.Path.GetFileName(PictureFile), Kind = SelectedSlider != null ? "Slider" : "Image", State = JsonConvert.SerializeObject(CaptureSessionData()) };
                LibraryItems.Add(item); activeLibraryItem = item; item.PropertyChanged += (_, _) => SaveLibrary();
            }
        } catch (Exception ex) { LibraryStatus = "Cannot restore library: " + ex.Message; }
        finally { switchingLibrary = false; libraryReady = true; }
        RaisePropertyChanged(nameof(ActiveLibraryItem));
    }
    // Capture current edits before serializing the full library; skip incomplete restore operations.
    private void SaveLibrary()
    {
        if (!libraryReady || switchingLibrary || loadingSession) return;
        if (activeLibraryItem != null) activeLibraryItem.State = JsonConvert.SerializeObject(CaptureSessionData());
        try { PicturatorStorage.Write(LibraryFile, JsonConvert.SerializeObject(new LibraryData { Items = LibraryItems.ToList(), ActiveId = activeLibraryItem?.Id }, Formatting.Indented)); }
        catch (Exception ex) { LibraryStatus = "Cannot save library: " + ex.Message; }
        RaisePropertyChanged(nameof(VisibleLayers));
    }
    private void AddLibraryState(SessionData state, string name, string kind)
    {
        var item = new PicturatorLibraryItem { Name = name, Kind = kind, State = JsonConvert.SerializeObject(state) };
        item.PropertyChanged += (_, _) => SaveLibrary();
        LibraryItems.Add(item); ActiveLibraryItem = item;
    }
    // Resolve copied editor timestamps or raw slider lines, then create editable library layers.
    public void ImportSliderText(string clipboardText)
    {
        var map = File.Exists(BeatmapPath) ? new BeatmapEditor(BeatmapPath).Beatmap : null;
        var sliders = EditorSliderSelection.Resolve(clipboardText, map);
        bool generated = sliders.Any(s => s.GetLine().Count(c => c == '|') > 2000);
        if (generated && MessageBox.Show(
            "This selection appears to contain a generated Slider Picturator slider. Import it? It may be slow and will not restore the original editable layers.",
            "Import Slider Picturator?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) {
            LibraryStatus = "Import cancelled. Your current library is unchanged.";
            return;
        }
        foreach (var slider in sliders) {
            var state = CaptureSessionData();
            state.SelectedSliderLine = slider.GetLine(); state.PictureFile = "";
            state.Effects.Cuts.Clear();
            state.TimeCode = slider.Time;
            state.Duration = slider.TemporalLength > 0 ? Math.Clamp(Math.Round(slider.TemporalLength * Math.Max(1, slider.Repeat)), 2, 60000) : 1000;
            state.SliderStartX = slider.Pos.X; state.SliderStartY = slider.Pos.Y; state.SliderScale = 1;
            state.BallPathSliderLine = ""; state.BallOffsetX = 0; state.BallOffsetY = 0; state.BallPathScale = 1;
            state.BallGraphEnabled = false; state.HasSliderBall = true;
            if (map != null) state.TargetCS = map.Difficulty["CircleSize"].DoubleValue;
            AddLibraryState(state, $"Slider • {TimeSpan.FromMilliseconds(slider.Time):mm\\:ss\\.fff}", "Slider");
        }
        LibraryStatus = $"Imported {sliders.Count} slider(s). Start times and durations loaded automatically" + (map == null ? "; choose the source map for accurate durations." : ".");
        SaveSession();
    }
    public Task LoadSelectedSlidersAsync()
    {
        ImportClipboardSliders();
        return Task.CompletedTask;
    }
    public void ImportClipboardSliders()
    {
        try {
            if (!File.Exists(BeatmapPath)) BrowseBeatmap();
            if (Clipboard.ContainsText()) ImportSliderText(Clipboard.GetText());
            else LibraryStatus = "Clipboard does not contain a slider selection.";
        } catch (Exception ex) { LibraryStatus = ex.Message; }
    }
    // Validate each image before adding it; failed files do not discard successful imports.
    public void ImportImages(IEnumerable<string> files)
    {
        int count = 0;
        var failures = new List<string>();
        foreach (var file in files) {
            try {
                using var check = new System.Drawing.Bitmap(file);
                var state = CaptureSessionData();
                state.SelectedSliderLine = ""; state.PictureFile = System.IO.Path.GetFullPath(file);
                state.Effects.Cuts.Clear();
                state.BallPathSliderLine = ""; state.HasSliderBall = false; state.BallGraphEnabled = false;
                state.BallOffsetX = 0; state.BallOffsetY = 0; state.BallPathScale = 1; state.SliderScale = 1;
                AddLibraryState(state, System.IO.Path.GetFileName(file), "Image"); count++;
            } catch (Exception ex) { failures.Add($"Cannot import {System.IO.Path.GetFileName(file)}: {ex.Message}"); }
        }
        if (count > 0) LibraryStatus = $"Imported {count} image(s). Images use the active start time; adjust it before exporting.";
        if (failures.Count > 0) LibraryStatus = $"Imported {count} image(s). " + string.Join(" ", failures);
        SaveSession();
    }
    // Release cached preview resources and select the next remaining layer.
    public void RemoveLibraryItem()
    {
        if (activeLibraryItem == null) return;
        int index = LibraryItems.IndexOf(activeLibraryItem);
        if (layerCache.Remove(activeLibraryItem.Id, out var removed)) { removed.Model.previewTokenSource?.Cancel(); removed.Model.Bm = null; }
        LibraryItems.Remove(activeLibraryItem); activeLibraryItem = null;
        if (LibraryItems.Count > 0) ActiveLibraryItem = LibraryItems[Math.Min(index, LibraryItems.Count - 1)];
        else { SelectedSlider = null; BallPathSlider = null; Bm = null; BmImage = null; PictureFile = ""; RegeneratePreview(); SegmentCount = 0; RaisePropertyChanged(nameof(ActiveLibraryItem)); }
        SaveSession();
    }
    // Copy the complete editable state into a new layer with a distinct identity.
    public void DuplicateLibraryItem()
    {
        if (activeLibraryItem == null) return;
        AddLibraryState(CaptureSessionData(), activeLibraryItem.Name + " (copy)", activeLibraryItem.Kind);
    }
}


