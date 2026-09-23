# Developer guide

This is Styx (StyxHavenVN)'s Mapping Tools extension for Slider Picturator and Sliderball.
Source comments explain the main entry points and why important steps exist.

## Data flow
1. SliderPicturatorVm.Library.cs resolves clipboard selections and adds independent layers.
2. Each layer stores its source, transforms, glitch settings, motion graph and polygon cuts.
3. AlignedScanlineGlitch.cs generates shader samples for body and border independently.
4. LayerEffects.cs applies cuts after the glitch. CutShapes.cs supplies normalized polygons.
5. CutEditorWindow.cs edits temporary polygons. Cancel discards them; Done sends them to the model.
6. PicturatorExporter.Capture freezes visible layers, GeneratePath creates geometry, Export writes the map.
7. PicturatorStorage writes sessions and libraries in LocalAppData, with .bak backups.

Do not move user data back beside the executable. Build output folders change between versions.
Legacy build data is archived before cleanup; do not delete the archive without checking its contents.

## Build and package (Windows x64)
dotnet publish StandalonePicturator.csproj -c Release -r win-x64 --self-contained true -p:UseAppHost=true -o artifacts/installer-payload
tools\InnoSetup\ISCC.exe packaging\installer.iss

The installer contains the .NET runtime and application dependencies; no separate runtime install is needed.
It installs per-user, adds shortcuts and an uninstaller. Uninstall leaves user-created session data alone.
Final installer: artifacts/setup/StyxSliderPicturator-Setup.exe
The installer is not digitally signed.

## Checks
dotnet run --project Tests/PicturatorRegression/PicturatorRegression.csproj -- --cuts-only
dotnet run --project Tests/PicturatorRegression/PicturatorRegression.csproj -- --aligned-glitch-only
dotnet run --project Tests/PicturatorRegression/PicturatorRegression.csproj -- --storage-only

For tests which create application models, set STYX_PICTURATOR_DATA to a temporary folder first.

