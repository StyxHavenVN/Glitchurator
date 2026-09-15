# Slider Picturator

Run `bin/Release/net10.0-windows/StandalonePicturator.exe`.

## Import selected sliders

1. Choose the source difficulty with **Choose map…** once.
2. Save your changes in osu! editor. Select one or more sliders and press Ctrl+C.
3. Click **Load copied sliders** in this tool. It reads the existing clipboard timestamp (for example `00:11:827 (2) -`) and loads the slider from the saved map. Start time, duration, original placement and shape are filled automatically.
4. **Import clipboard** is an alternative button for the same import workflow.

The editor clipboard contains timestamps and combo numbers, not the edited geometry. The selected `.osu` file must be the same saved difficulty. The first timestamp takes priority over combo numbering. A unique match within 1 ms is accepted for rounding; more distant timestamps are rejected. Raw hitobject lines are also accepted by Import clipboard; source timing is needed for accurate durations.

## Slider & image library

The right-hand list contains image layers. All visible layers appear together in one preview; drag the selected layer to overlap other layers. Use each Show / Hide checkbox to include or exclude that layer from the preview and export. Layers later in the list draw on top; selecting a layer does not reorder it. Select an item to edit it. Each item retains its own position, scale, ball path, graph, timing, native shading and glitch settings. Use **Item name**, **Duplicate** and **Remove** to manage entries. **INJECT SLIDER** combines all visible layers into one slider, using the selected layer's timing, duration and ball path. Each layer retains its native/glitch shading. The graph's total segment calculation includes the combined image; the left scanline label counts only the selected layer. Visibility is saved with the library.

**Import PNG / images…** supports multiple files. You can also drop PNG files onto the library. Images do not have beatmap timestamps: they inherit the active start time, which you can adjust before exporting. Keep source images at their imported paths so they remain available after restarting.

The library is saved to `picturator_library.json` beside the application. The active session is also saved to `picturator_session.json`. Existing single-item sessions are imported into the library automatically. When moving between Debug and Release, copy both files if you want to carry over the library.

## Preview and ball path

- Drag the image to move it. Drag the turquoise corner or scroll to scale it. Ctrl+scroll changes shape thickness (CS).
- Enable **Sliderball follows shape** to show the movable path panel. Drag its title to reposition the panel.
- Shift+drag, drag the ball, or drag the yellow frame edge to move the ball path independently. The panel also accepts X/Y offsets and path scale.
- **Reset path alignment** restores the shape path and clears separate offsets/scale. For an imported image, use **Import separate path (Ctrl+C)** with exactly one copied slider.
- Use **Play**, the time scrubber, and **Fit view** to inspect movement.
- **Match osu! resolution automatically** reads the installed osu! configuration. If unavailable, the entered vertical resolution is retained. Correct resolution is essential for matching exported body and ball placement.

Shape thickness is independent of the native ball size, which comes from beatmap CS. The preview shows placement and approximate shading; verify the final shader/glitch appearance in osu!.

## Graph and segments

Open **GRAPH — sliderball time / position…**. Add or drag points to set position along the path over time. Drag the middle handles to bend a segment. Right-click for curve choices; hold Shift to snap to 1/16. Linear and Steps presets, numerical point editing and live playback are available.

**Minimum tumour length (1–12)** controls off-image pacing segments. Higher values reduce segment count; lower values produce more segments. This is a relative length, not original slider pixels. **Calculate segments** uses the same path generator as export. The image scanlines and motion samples impose a minimum count; the control is not an FPS guarantee.

## Native appearance and glitch

**Native gradient body and border** gives the body its continuous radial shading and native border. It does not insert glitch cuts. **Enable glitch** separately controls displacement, frequency, spike thickness and randomization. Actual colors depend on the osu! skin and selection state.

## Other tabs

**Giant & Matrix Slider** provides experimental extreme-coordinate Catmull and matrix presets. Choose a map, edit anchors or use a preset, then generate and export. The anchor diagram does not emulate GPU artifacts; matching a particular matrix appearance requires testing in osu!. Export recomputes length from rounded anchors, so it may differ from a pasted original.

**Tick Art** creates a native slider with ticks along a drawn path. Click to place points or drag to draw; right-click removes the last point. Set time, duration and tick spacing, then update the preview and export. Changing the exported map's global tick rate is opt-in and affects its other sliders too. The preview does not reproduce skin glow.

Exports preserve neighboring objects and restore timing/SV after the inserted object's timing setup. Existing destination files are backed up to `.bak`. Giant/Matrix and Tick Art can export to a separate `.osu` file. Picturator's Inject writes to the chosen map.

## Verification

Build with `dotnet build StandalonePicturator.csproj --no-restore -c Release`.

Focused regression checks:

```powershell
dotnet run --project Tests/PicturatorRegression/PicturatorRegression.csproj --no-restore -p:UseAppHost=false -- --library-only
dotnet run --project Tests/PicturatorRegression/PicturatorRegression.csproj --no-restore -p:UseAppHost=false -- --segment-only
dotnet run --project Tests/PicturatorRegression/PicturatorRegression.csproj --no-restore -p:UseAppHost=false -- --tick-only
dotnet run --project Tests/PicturatorRegression/PicturatorRegression.csproj --no-restore -p:UseAppHost=false -- --extreme-only
```

Library tests cover timestamp selection, repeat duration, independent edits, PNG import, duplication, persistence, removal and WPF rendering. The automated tests do not send Ctrl+C to a live osu! editor.
