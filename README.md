# Glitchurator

**An extension of Mapping Tools' Slider Picturator and Sliderball functionality for osu!, developed by Styx (StyxHavenVN).**

Glitchurator lets you combine sliders and images into a single image, create glitch effects, adjust sliderball paths, and export the result to an `.osu` beatmap.

## Features

- Import one or more sliders from the osu! editor using the clipboard.
- Import PNG images, combine multiple layers, and manage them with Show / Hide, Duplicate, and Remove.
- Create gradient bodies, native borders, and layered scanline glitches.
- Rotate glitch effects independently for Body, Border, or Both.
- Cut shapes using rectangles, squares, ellipses, triangles, stars, hearts, and freehand selections.
- Edit sliderball movement using a time/position graph.
- Create multi-ball illusions and export hidden-body sliders.
- Share the selected beatmap across tabs.

## Installation

### Requirements

- 64-bit Windows.
- osu! and the `.osu` beatmap you want to edit. The special rendering techniques target **osu! Stable**.
- The **Glitchurator-Setup.exe** installer.

The installer includes the .NET runtime and required libraries. You do not need to install each library separately.

### Steps

1. Open `Glitchurator-Setup.exe`.
2. Follow the installer and click **Install**.
3. Click **Finish**, then open **Glitchurator** from the Desktop or Start Menu.

## Quick Start

1. Save your beatmap in the osu! editor.
2. Open Glitchurator, click **Choose map…** under **Shared beatmap .osu**, and select the correct difficulty.
3. In the osu! editor, select a slider and press **Ctrl+C**.
4. Return to the application and click **Load copied sliders**.
5. Adjust the shape, glitch effect, or ball path in the preview.
6. Check the start time, duration, and resolution.
7. Click **INJECT SLIDER**.
8. Reload the beatmap in osu! to check the result.

> **INJECT SLIDER writes to the selected `.osu` file.** The application creates a `.bak` copy before writing. Testing on a separate difficulty is recommended.

## Importing Sliders and Images

### Sliders from the osu! Editor

Select the correct `.osu` file, save the map in the editor, select one or more sliders, and press **Ctrl+C**. Then click **Load copied sliders** or **Import clipboard**.

### PNG Images

- Click **Import PNG / images…** or **Import images…** to select images.
- You can import multiple images or drop PNG files into the library list.
- Images have no timing of their own; check the start time and duration before exporting.
- Keep source images at their imported paths so the session can load them again.

## Library and Preview

The **SLIDER & IMAGE LIBRARY** list is on the right.

| Action | Effect |
| --- | --- |
| Select an item | Edit that layer |
| Show / Hide | Include or exclude the layer from the preview and combined output |
| Item name | Rename the layer |
| Duplicate | Duplicate the layer and its settings |
| Remove | Remove the layer from the library |
| Drag the image | Move the layer |
| Drag the cyan square / scroll | Adjust the scale |
| Ctrl + scroll | Adjust the shape's CS thickness |
| Play | Preview movement |
| Fit view | Fit the content into the viewing area |

Visible layers are combined into **one output slider**. Timing and duration come from the selected layer. By default, the ball path belongs to the selected layer; multi-ball uses multiple valid visible paths.

**Match osu! resolution automatically** attempts to detect the osu! resolution. If detection fails, enter the correct vertical resolution. The preview approximates layout and shading; osu! skins and renderers may produce different results.

## Native Appearance and Glitch

**Native gradient body and border** creates a gradient body and native border. **Enable glitch** controls the standard glitch effect. **Layered scanline glitch** is a separate mode for layered scanlines across the body and border.

| Setting | Effect |
| --- | --- |
| Direction | Direction of the glitch streaks; 0° is horizontal |
| Displacement | Displacement strength |
| Overall density | Overall glitch density |
| Spike thickness | Streak thickness |
| Randomize glitch | Generate a different random pattern |
| Random rows | Enable randomized rows; disable for evenly aligned rows |
| Row spacing | Distance between rows |
| Tiers | Number of layers |
| Outer density / Middle density | Density of the outer / middle regions |
| Solid inner width | Adjust the core region and the effect on the body |

### Rotating the Body and Border Independently

Under **Rotate layered glitch**, select **Body**, **Border**, or **Both**, then adjust **Direction**. Each component stores its own angle; rotating one preserves the other component's settings. Selecting Both sets the same angle for both.

## Cutting Shapes

1. Right-click the selected layer in the preview → **Cut…**.
2. The **Cut editor** window has a large image area at the top and tools at the bottom.
3. Select Rectangle, Square, Ellipse, Triangle, Star, Heart, or Freehand.
4. Drag to create a selection; drag inside it to move it, or drag its corners to resize it.
5. Click **Cut selection** or press **Delete** to remove the area **inside** the selection.
6. Continue cutting if needed, then click **Done** to save.

**Freehand** automatically connects the last point to the first to form a closed region.

- **Ctrl+Z / Undo:** clear the current selection; if there is no selection, undo the most recent cut in the window.
- **Reset:** discard the temporary cuts made during this editing session.
- **Esc:** clear the selection.
- **Cancel:** discard changes made since opening the window.
- After closing the window, use **Undo cut** in the preview's right-click menu to undo a saved cut.

Cuts only affect the layer's image; the sliderball path is not cut.

## Sliderball and Graph

### Ball Path

1. Enable **Sliderball follows shape**.
2. Use the **Sliderball path** panel to adjust X/Y offsets and path scale.
