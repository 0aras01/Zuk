# Fractal Explorer E2E Test Suite Design Plan

This document details the read-only exploration findings and a comprehensive, 4-tier opaque-box end-to-end (E2E) integration test suite plan for the Fractal Explorer application. It is designed to verify all presentation-layer, viewport navigation, rendering, and interaction logic in an isolated, repeatable manner without requiring full UI automation drivers.

---

## 1. Codebase Analysis & Current Status

### 1.1 Architectural Overview
The presentation layer of Fractal Explorer is built using the Model-View-ViewModel (MVVM) pattern with Avalonia UI. It is structured around the following components:
- **MainViewModel.cs**: The central coordinator and data context for `MainWindow`. It aggregates all the commands, coordinates with rendering/panning/zooming pipelines, and exposes properties for data binding.
- **ZoomService.cs**: Implements `IZoomService` and encapsulates the viewport boundaries (`ComplexPlane` coordinates), image sizes, aspect ratio corrections (`AdjustAspectRatio`), and a history stack of viewports to support zooming out.
- **BookmarkService.cs**: Handles persistence of bookmark settings to a local JSON file (`bookmarks.json`) located in `Environment.SpecialFolder.ApplicationData/FractalExplorer`.
- **MainWindow.axaml.cs**: Directs raw user input events (pointer clicks, drags, mouse wheel, key presses) and translates them into method calls and commands on the `MainViewModel`.
- **IFractalGenerator / ParallelFractalGenerator / ILGPUFractalGenerator**: Handle the math calculations. The CPU implementation is parallelized with perturbation math. The GPU generator uses ILGPU compilation to target graphics hardware, fallback-safe to CPU.

### 1.2 Compilation & Verification Status
- Checked the build status of the workspace by executing `dotnet test` in the root workspace.
- **Result**: Successfully compiled and executed the existing test suite (34 passing, 0 failing, 0 skipped).
- Existing tests:
  - `Fractal.Tests/Core/*.cs` verify `CoordinateMapper`, `DoubleDouble` arithmetic, `FractalCalculator`, `PerturbationEngine`, and `ZoomService`.
  - `Fractal.Tests/UI/MainViewModelTests.cs` verifies localized pointer selections and size change command delegates using `Moq` for service dependencies.
  - `Fractal.Tests/UI/LocalizationServiceTests.cs` verifies language string retrieval in English and Polish.

---

## 2. E2E Test Suite Design (4-Tier Approach)

To test the system as an opaque box, the test suite exercises the public interface of `MainViewModel`, simulating direct user keyboard and mouse inputs, and asserting on the exposed properties and rendered pixel buffers.

### Tier 1: Feature Coverage (Feature-by-Feature)
At least 5 tests are defined for each of the 11 identified application features.

#### Feature 1: Zooming
1. **`Zoom_MouseWheelZoomIn_IncreasesZoomFactor`**:
   - *Description*: Simulate wheel scroll up at the center of the viewport.
   - *Interaction*: Call `OnMouseWheelZoom(new Point(400, 300), 1.0)`.
   - *Assertion*: Viewport span shrinks, `ZoomText` updates to a higher value (e.g., `2.0x`), and `CanZoomOut` becomes true.
2. **`Zoom_MouseWheelZoomOut_DecreasesZoomFactor`**:
   - *Description*: Simulate wheel scroll down.
   - *Interaction*: Call `OnMouseWheelZoom(new Point(400, 300), -1.0)`.
   - *Assertion*: Viewport span expands, and `ZoomText` updates.
3. **`Zoom_CenteredZoomInCommand_IncreasesZoomFactor`**:
   - *Description*: Execute centered zoom-in command (keyboard shortcut key `+`).
   - *Interaction*: Call `ZoomCentered(zoomIn: true)`.
   - *Assertion*: The complex plane range is halved, keeping the exact center coordinates constant.
4. **`Zoom_CenteredZoomOutCommand_DecreasesZoomFactor`**:
   - *Description*: Execute centered zoom-out command (keyboard shortcut key `-`).
   - *Interaction*: Call `ZoomCentered(zoomIn: false)`.
   - *Assertion*: The complex plane range is doubled, keeping the center coordinates constant.
5. **`Zoom_HistoryNavigation_CanZoomOutIncreasesAndDecreases`**:
   - *Description*: Verify history depth updates correctly.
   - *Interaction*: Zoom in three times via `ZoomCentered(true)`, then execute `ZoomOutCommand` three times.
   - *Assertion*: `CanZoomOut` is initially false, becomes true on first zoom, remains true during zooming, and becomes false after the third zoom-out, returning the viewport to default.

#### Feature 2: Panning
1. **`Pan_MiddleButtonMouseDrag_UpdatesCoordinates`**:
   - *Description*: Simulate middle-click drag to pan the fractal view.
   - *Interaction*: Call `StartPan(new Point(100, 100))`, then `MovePan(new Point(150, 120))`, then `EndPan()`.
   - *Assertion*: Viewport center coordinates shift by the delta represented in the complex plane; rendering is requested.
2. **`Pan_ArrowKeys_PansViewportCorrectly`**:
   - *Description*: Simulate arrow key pans (Keyboard Left/Right/Up/Down).
   - *Interaction*: Call `PanByPercent(-0.1, 0)` (Left) and `PanByPercent(0, 0.1)` (Up).
   - *Assertion*: Real range shifts left by 10% and Imaginary range shifts up by 10%.
3. **`Pan_DebounceTimer_DelaysRenderRequests`**:
   - *Description*: Verify drag movement doesn't choke the renderer with infinite updates.
   - *Interaction*: Call `StartPan(100,100)`, quickly trigger `MovePan` 5 times in less than 50ms, then wait.
   - *Assertion*: Verify that intermediate steps do not spawn concurrent render cycles, but debounced rendering executes after drag halts.
4. **`Pan_CancelActivePanState_RetainsFinalCoordinates`**:
   - *Description*: Verify pointer release coordinates finalize pan.
   - *Interaction*: Call `StartPan(0,0)`, `MovePan(10,10)`, then `EndPan()`.
   - *Assertion*: `IsPanning` becomes false, and the final viewport corresponds to the offset at `(10,10)`.
5. **`Pan_MultipleConsecutivePans_AccumulatesOffset`**:
   - *Description*: Drag, release, drag again.
   - *Interaction*: Execute two separate pan sequences sequentially.
   - *Assertion*: Coordinates shift cumulatively; final center is exactly the sum of both pan vectors.

#### Feature 3: Selection (Box Selection)
1. **`Selection_PointerDownAndDrag_CreatesSelectionRectangle`**:
   - *Description*: Drag pointer to define a zoom region.
   - *Interaction*: Call `OnPointerPressed(new Point(100, 100))`, `OnPointerMoved(new Point(300, 200))`.
   - *Assertion*: `IsSelecting` is true, and `SelectionRectangle` has width `200` and height `100`.
2. **`Selection_PointerReleased_ZoomsToSelectedRegion`**:
   - *Description*: Complete selection.
   - *Interaction*: Call `OnPointerReleased(new Point(300, 200))` after dragging.
   - *Assertion*: Viewport shifts to the selected coordinates, `IsSelecting` becomes false, and render triggers.
3. **`Selection_CancelSelectionCommand_ClearsSelection`**:
   - *Description*: Cancel box selection via Escape key command.
   - *Interaction*: Start selection, then call `CancelSelection()`.
   - *Assertion*: `IsSelecting` becomes false; selection coordinates reset; viewport plane remains unchanged.
4. **`Selection_TinySelection_IsIgnored`**:
   - *Description*: Prevent accidental tiny zoom boxes.
   - *Interaction*: Draw selection from `(100, 100)` to `(105, 105)` (5x5 pixels) and release.
   - *Assertion*: Pointer release does not alter viewport coordinates (ignored due to size < 10 pixels).
5. **`Selection_ReverseDragSelection_CalculatesPositiveRectangle`**:
   - *Description*: Drag from bottom-right to top-left.
   - *Interaction*: Press at `(300, 200)`, drag to `(100, 100)`, release.
   - *Assertion*: `SelectionRectangle` maintains positive width (200) and height (100); view successfully zooms to that region.

#### Feature 4: Reset
1. **`Reset_ViewModelCommand_RestoresDefaultPlane`**:
   - *Description*: Reset view to default.
   - *Interaction*: Call `ResetCommand.Execute(null)`.
   - *Assertion*: Viewport plane matches the initial default bounds (adjusted for aspect ratio).
2. **`Reset_ClearsZoomHistoryStack`**:
   - *Description*: Ensure history is emptied on reset.
   - *Interaction*: Zoom in several times, then trigger `ResetCommand`.
   - *Assertion*: `CanZoomOut` becomes false.
3. **`Reset_TriggersRenderRequest`**:
   - *Description*: Verify reset starts drawing immediately.
   - *Interaction*: Monitor rendering execution when executing reset.
   - *Assertion*: A new render cycle is dispatched for the default viewport coordinates.
4. **`Reset_KeysShortcut_ResetsCorrectly`**:
   - *Description*: Verify shortcut handler logic works.
   - *Interaction*: Trigger keyboard shortcut 'R' command.
   - *Assertion*: Viewport returns to default bounds.
5. **`Reset_RestoresAdaptiveIterationsToDefault`**:
   - *Description*: Verify iterations are not corrupted.
   - *Interaction*: Allow adaptive iterations to adjust during zooms, then trigger `ResetCommand`.
   - *Assertion*: Telemetry and calculation parameters stay within boundaries.

#### Feature 5: Bookmarks
1. **`Bookmarks_LoadDefaultBookmarks_PopulatesList`**:
   - *Description*: Verify preset valley configurations load.
   - *Interaction*: Initialize ViewModel.
   - *Assertion*: `Bookmarks` collection contains "Seahorse Valley", "Elephant Valley", "Triple Spiral Valley", and "Julia Default".
2. **`Bookmarks_SelectBookmark_UpdatesViewModelProperties`**:
   - *Description*: Select a bookmark from the list.
   - *Interaction*: Set `SelectedBookmark` to the "Seahorse Valley" entry.
   - *Assertion*: `SelectedFractalType` matches the bookmark's type, view zooms to the saved coordinates, iterations update, and a render is requested.
3. **`Bookmarks_AddCustomBookmark_AppendsToCollection`**:
   - *Description*: Add current viewport as a bookmark.
   - *Interaction*: Set `NewBookmarkName = "My Test Valley"`, execute `AddBookmarkCommand`.
   - *Assertion*: A new bookmark entry is appended to the collection matching the name and current viewport parameters.
4. **`Bookmarks_DeleteBookmark_RemovesFromCollection`**:
   - *Description*: Delete a bookmark.
   - *Interaction*: Call `DeleteBookmarkCommand` with the custom bookmark.
   - *Assertion*: The bookmark is removed from the collection and `SelectedBookmark` is cleared if it matched.
5. **`Bookmarks_AddBookmarkSave_PersistsToFile`**:
   - *Description*: Verify changes persist.
   - *Interaction*: Add a new bookmark.
   - *Assertion*: The json bookmarks file is written to disk containing the new entry.

#### Feature 6: Localization
1. **`Localization_SetCultureToPolish_TranslatesUIStrings`**:
   - *Description*: Switch UI culture to Polish.
   - *Interaction*: Set `SelectedLanguage = "PL"`.
   - *Assertion*: `LocalizationService.Instance.CurrentCulture` is set to "pl", and localized indexer lookups return Polish translations (e.g., `Instance["AppName"] == "Eksplorator Fraktali"`).
2. **`Localization_SetCultureToEnglish_TranslatesUIStrings`**:
   - *Description*: Switch UI culture to English.
   - *Interaction*: Set `SelectedLanguage = "EN"`.
   - *Assertion*: CurrentCulture is set to "en", and indexer returns English values.
3. **`Localization_SelectedLanguageChanged_UpdatesCulture`**:
   - *Description*: Verify VM property triggers change.
   - *Interaction*: Update `SelectedLanguage` to "PL".
   - *Assertion*: A `PropertyChanged` notification is fired for the UI elements to update bindings.
4. **`Localization_CurrentCultureMatchesSystemCulture_OnStartup`**:
   - *Description*: Verify default startup translation selection.
   - *Interaction*: Initialize service with system culture set to Polish.
   - *Assertion*: Language defaults to "PL".
5. **`Localization_IndexerReturnsKeyName_ForMissingResourceKey`**:
   - *Description*: Check fallback behavior.
   - *Interaction*: Request key `"NonExistentKey_XYZ"`.
   - *Assertion*: Returns `"NonExistentKey_XYZ"` instead of throwing an exception.

#### Feature 7: Presets / Palettes / Fractal Types
1. **`Presets_ChangeSelectedPalette_TriggersReRender`**:
   - *Description*: Switch palettes.
   - *Interaction*: Set `SelectedPalette = PaletteType.Ice`.
   - *Assertion*: Generates new image data using the colors specified by the Ice palette.
2. **`Presets_ChangeSelectedFractalTypeToJulia_ShowsJuliaSettings`**:
   - *Description*: Switch fractal to Julia.
   - *Interaction*: Set `SelectedFractalType = FractalType.Julia`.
   - *Assertion*: `IsJuliaSettingsVisible` becomes true, settings panel displays, and a Julia render dispatches.
3. **`Presets_ChangeSelectedFractalTypeToMandelbrot_HidesJuliaSettings`**:
   - *Description*: Switch fractal back to Mandelbrot.
   - *Interaction*: Set `SelectedFractalType = FractalType.Mandelbrot`.
   - *Assertion*: `IsJuliaSettingsVisible` becomes false, settings panel hides, and rendering starts.
4. **`Presets_QuickPaletteHotkeys_UpdatesSelectedPalette`**:
   - *Description*: Simulate number key bindings.
   - *Interaction*: Trigger keyboard shortcut command for '2'.
   - *Assertion*: `SelectedPalette` changes to `PaletteType.Ice`.
5. **`Presets_AvailablePalettesList_MatchesEnumValues`**:
   - *Description*: Verify all palettes are exposed to UI dropdown.
   - *Interaction*: Inspect `Palettes` collection.
   - *Assertion*: Contains all four PaletteType values (Sunset, Ice, Rainbow, Forest).

#### Feature 8: Julia Parameter Tuning
1. **`Julia_UpdateRealCoordinate_TriggersRender`**:
   - *Description*: Change Julia real parameter input.
   - *Interaction*: Set `JuliaReal = "-0.8"`.
   - *Assertion*: Render dispatches with the updated constant value.
2. **`Julia_UpdateImaginaryCoordinate_TriggersRender`**:
   - *Description*: Change Julia imaginary parameter input.
   - *Interaction*: Set `JuliaImag = "0.156"`.
   - *Assertion*: Render dispatches.
3. **`Julia_ValidCoordinates_ParsedCorrectly`**:
   - *Description*: Verify numeric parsing.
   - *Interaction*: Enter `"-0.7011"`.
   - *Assertion*: Parses to double-double structure without formatting exceptions.
4. **`Julia_InvalidRealCoordinate_FallsBackToDefault`**:
   - *Description*: Input non-numeric characters into Julia coordinate input box.
   - *Interaction*: Set `JuliaReal = "invalid_text"`.
   - *Assertion*: Rendering runs using the fallback real coordinate `-0.7` instead of crashing.
5. **`Julia_InvalidImaginaryCoordinate_FallsBackToDefault`**:
   - *Description*: Input special characters into imaginary coordinate.
   - *Interaction*: Set `JuliaImag = "+++123"`.
   - *Assertion*: Rendering runs using fallback imaginary coordinate `0.27015`.

#### Feature 9: Animation
1. **`Animation_ToggleStart_SetsIsAnimatingToTrue`**:
   - *Description*: Start auto zoom animation.
   - *Interaction*: Call `ToggleAnimationCommand.Execute(null)`.
   - *Assertion*: `IsAnimating` becomes true; animation task starts.
2. **`Animation_ToggleStop_SetsIsAnimatingToFalse`**:
   - *Description*: Stop active animation.
   - *Interaction*: Call `ToggleAnimationCommand` while animating.
   - *Assertion*: `IsAnimating` becomes false.
3. **`Animation_LoopZoom_ModifiesComplexPlane`**:
   - *Description*: Verify zoom level increments per frame.
   - *Interaction*: Allow animation loop to execute for two steps.
   - *Assertion*: Viewport boundaries decrease by 3% each iteration.
4. **`Animation_LoopZoom_TriggersGenerations`**:
   - *Description*: Verify frames are drawn.
   - *Interaction*: Track render invocations during active animation.
   - *Assertion*: Multiple asynchronous generates execute consecutively.
5. **`Animation_LoopZoom_PushesToZoomHistory`**:
   - *Description*: View history stack depth increases.
   - *Interaction*: Run animation for 3 frames.
   - *Assertion*: `CanZoomOut` becomes true, and history stack accumulates the viewports.

#### Feature 10: Diagnostics Panel
1. **`Diagnostics_ToggleVisibilityCommand_ChangesProperty`**:
   - *Description*: Hide/Show diagnostics HUD.
   - *Interaction*: Toggle `IsDiagnosticsVisible` (simulated by key 'D').
   - *Assertion*: State toggles from true to false.
2. **`Diagnostics_RenderCompletion_UpdatesTelemetryStrings`**:
   - *Description*: Verify HUD statistics populate correctly.
   - *Interaction*: Await a rendering task completion.
   - *Assertion*: `CenterCoordinatesText`, `SpanText`, `ResolutionText`, `RenderTimeText`, `IterationsText`, `EngineText`, and `ZoomText` update to display the latest render statistics.
3. **`Diagnostics_AdaptiveIterations_IncreasesWhenRenderIsFast`**:
   - *Description*: Benchmark speed tuning (Fast path).
   - *Interaction*: Simulate a render that takes 10ms (well under the 100ms target).
   - *Assertion*: The iteration budget increases for the subsequent render to improve details.
4. **`Diagnostics_AdaptiveIterations_DecreasesWhenRenderIsSlow`**:
   - *Description*: Benchmark speed tuning (Slow path).
   - *Interaction*: Simulate a render that takes 500ms.
   - *Assertion*: The iteration budget decreases for the next render to maintain target performance.
5. **`Diagnostics_TelemetryDisplaysZeroWarmUpRenderTime`**:
   - *Description*: Telemetry rendering formats check.
   - *Interaction*: Await a complete render cycle.
   - *Assertion*: `RenderTimeText` has valid suffix `" ms"` and numeric value greater than or equal to 0.

#### Feature 11: File Export & Clipboard
1. **`Export_SaveImageCommand_SavesPngFile`**:
   - *Description*: Save image to custom path.
   - *Interaction*: Setup `SaveFileDialogAction` to return `"test_capture.png"`, then call `SaveImageCommand`.
   - *Assertion*: Target file is written to the local disk and `StatusText` displays success.
2. **`Export_SaveImageCommandFallback_AutoSavesFile`**:
   - *Description*: Auto-save fallback path.
   - *Interaction*: Set `SaveFileDialogAction = null`, then call `SaveImageCommand`.
   - *Assertion*: File is written inside a subfolder named `SavedImages` inside the execution directory.
3. **`Export_CopyToClipboardCommand_InvokesAction`**:
   - *Description*: Copy rendered image.
   - *Interaction*: Wire up a simulated `CopyToClipboardAction` delegate, trigger `CopyToClipboardCommand`.
   - *Assertion*: The clipboard copy action is executed successfully.
4. **`Export_SaveImageCommand_NoImageDoesNothing`**:
   - *Description*: Empty image state check.
   - *Interaction*: Force `FractalImage = null`, call `SaveImageCommand`.
   - *Assertion*: File dialog delegate is not invoked; no files are saved.
5. **`Export_CopyToClipboardCommand_NoImageDoesNothing`**:
   - *Description*: Empty clipboard state check.
   - *Interaction*: Force `FractalImage = null`, call `CopyToClipboardCommand`.
   - *Assertion*: Clipboard copy delegate is not invoked.

---

### Tier 2: Boundary & Corner Cases

#### Feature 1: Zooming
1. **`Zoom_ExtremelyDeepZoom_SwitchesToCpuGenerator`**:
   - *Description*: Verify precision engine fallback.
   - *Interaction*: Zoom repeatedly until zoom factor exceeds $10^{10}$.
   - *Assertion*: Generator switches to CPU because double-double precision coordinates lose GPU hardware acceleration support.
2. **`Zoom_ZeroOrNegativeDimensions_DoesNotThrow`**:
   - *Description*: Window dimensions set to zero or negative.
   - *Interaction*: Set `ViewportWidth = 0` and `ViewportHeight = -10`, then trigger ZoomCentered.
   - *Assertion*: Math handlers clamp or preserve coordinates rather than dividing by zero or throwing.
3. **`Zoom_OutOfBoundsCursorPosition_ClampsOrHandlesZoom`**:
   - *Description*: Mouse wheel zoom with cursor coordinate outside image.
   - *Interaction*: Call `OnMouseWheelZoom(new Point(1000, -50), 1.0)`.
   - *Assertion*: Center coordinates map correctly (clamped to viewport edges or mapped to virtual canvas boundaries) without crashing.
4. **`Zoom_RepeatedZoomOutAtBaseLevel_DoesNotThrowOrChangeViewport`**:
   - *Description*: Zoom out command with empty history stack.
   - *Interaction*: Ensure `CanZoomOut` is false, then call `ZoomOutCommand` repeatedly.
   - *Assertion*: No operations occur, history remains empty, and view coordinates do not corrupt.
5. **`Zoom_PreserveAspectRatioOnResize_AdjustsPlane`**:
   - *Description*: Viewport dimensions transition (aspect ratio change).
   - *Interaction*: Call `OnSizeChanged(800, 600)`, then resize to `(800, 300)`.
   - *Assertion*: Aspect ratio adjuster updates the complex plane imaginary limits to double their size to preserve visual ratio.

#### Feature 2: Panning
1. **`Pan_ZeroDeltaDrag_DoesNotShiftCoordinates`**:
   - *Description*: Start and release pan at the exact same location.
   - *Interaction*: Call `StartPan(100, 100)`, `MovePan(100, 100)`, `EndPan()`.
   - *Assertion*: Complex plane coordinates remain identical to original.
2. **`Pan_ExtremelyLargeDrag_PansCorrectly`**:
   - *Description*: Drag mouse far outside viewport boundaries.
   - *Interaction*: Start at `(400, 300)`, drag to `(50000, -30000)`.
   - *Assertion*: Coordinate shift handles extremely large numeric values safely.
3. **`Pan_ArrowKeyPanAtDeepZoom_MaintainsResolution`**:
   - *Description*: Pan at $10^{15}$ zoom depth.
   - *Interaction*: Zoom deep, then call `PanByPercent(0.1, 0)`.
   - *Assertion*: Double-double precision keeps the pan step small enough ($10^{-16}$) to retain position alignment.
4. **`Pan_StartPanWithoutEndPan_ConcludesGracefully`**:
   - *Description*: Simulate size change or reset without completing drag.
   - *Interaction*: Call `StartPan(100,100)`, `MovePan(120,120)`, then call `ResetCommand` without calling `EndPan`.
   - *Assertion*: Pan state cancels cleanly; viewport successfully resets to original.
5. **`Pan_InvalidSizeDuringPan_HandlesGracefully`**:
   - *Description*: Window minimizes during active pan drag.
   - *Interaction*: Call `StartPan(10,10)`, then `OnSizeChanged(0, 0)`, then `MovePan(20,20)`.
   - *Assertion*: Panning logic ignores calculations or returns early due to zero size, avoiding division by zero.

#### Feature 3: Selection (Box Selection)
1. **`Selection_ExactBoundarySelection_SelectsCompleteViewport`**:
   - *Description*: Select exact coordinates of the viewport canvas.
   - *Interaction*: Drag from `(0, 0)` to `(ViewportWidth, ViewportHeight)`.
   - *Assertion*: The resulting zoom bounds align exactly with the current viewport boundaries.
2. **`Selection_DragOutsideViewport_ClampsOrHandlesGracefully`**:
   - *Description*: Drag box past edge of canvas.
   - *Interaction*: Press at `(10, 10)`, move to `(ViewportWidth + 100, ViewportHeight + 100)`.
   - *Assertion*: Clamps coordinates to screen edges or computes virtual bounds safely.
3. **`Selection_ZeroWidthHeightSelectionRelease_DoesNotZoom`**:
   - *Description*: Mouse click without drag.
   - *Interaction*: Press at `(100, 100)`, release at `(100, 100)`.
   - *Assertion*: Viewed plane is unaffected, and render is not redundantly triggered.
4. **`Selection_AspectMultiplierAdjustment_CorrectsPlaneSelection`**:
   - *Description*: Draw extremely narrow horizontal rectangle.
   - *Interaction*: Press at `(10, 10)`, drag to `(300, 12)`, release.
   - *Assertion*: Shorter (vertical) imaginary axis is expanded so the aspect ratio matches the canvas.
5. **`Selection_CancellationViaEscKey_ResetsState`**:
   - *Description*: Keyboard cancellation of active selection box.
   - *Interaction*: Press at `(10, 10)`, drag to `(100, 100)`. Trigger `Escape` key command path.
   - *Assertion*: `IsSelecting` becomes false; selection rectangle clears.

#### Feature 4: Reset
1. **`Reset_AfterExtremelyDeepZoom_Succeeds`**:
   - *Description*: Reset from deep zoom.
   - *Interaction*: Zoom to $10^{-15}$ range, execute `ResetCommand`.
   - *Assertion*: Coordinates restore to default range exactly; no precision remnants left.
2. **`Reset_WhenAlreadyAtResetState_DoesNothingDoubleReset`**:
   - *Description*: Reset multiple times in sequence.
   - *Interaction*: Trigger `ResetCommand` twice in a row.
   - *Assertion*: System remains stable, `CanZoomOut` stays false, and no duplicate renders are run.
3. **`Reset_WithZeroViewportDimensions_DoesNotThrow`**:
   - *Description*: Reset when window size is zero.
   - *Interaction*: Set `ViewportWidth = 0`, `ViewportHeight = 0`, execute `ResetCommand`.
   - *Assertion*: Safe execution of default viewport initialization.
4. **`Reset_DuringActivePan_ResetsViewportCorrectly`**:
   - *Description*: Reset while mid-pan.
   - *Interaction*: Set `IsPanning = true`, call `ResetCommand`.
   - *Assertion*: `IsPanning` is forced to false; default bounds restore.
5. **`Reset_DuringActiveAnimation_StopsAnimationAndResets`**:
   - *Description*: Reset while animating.
   - *Interaction*: Set `IsAnimating = true`, call `ResetCommand`.
   - *Assertion*: `IsAnimating` becomes false; animation loop halts; default bounds restored.

#### Feature 5: Bookmarks
1. **`Bookmarks_AddEmptyOrWhitespaceName_IsDisabled`**:
   - *Description*: Empty bookmark name validation.
   - *Interaction*: Set `NewBookmarkName = "   "`.
   - *Assertion*: `AddBookmarkCommand.CanExecute()` returns false.
2. **`Bookmarks_DeleteCurrentlySelectedBookmark_ClearsSelection`**:
   - *Description*: Delete the active bookmark.
   - *Interaction*: Select "Julia Default" bookmark, then call `DeleteBookmarkCommand` for it.
   - *Assertion*: Selected entry is removed from the collection and `SelectedBookmark` becomes null.
3. **`Bookmarks_SelectBookmarkWithInvalidCoords_HandlesGracefully`**:
   - *Description*: Selected bookmark has corrupted (NaN/infinite) boundaries.
   - *Interaction*: Create a bookmark entry containing `Plane = new ComplexPlane(double.NaN, 1, 0, 1)`. Select it.
   - *Assertion*: Handled gracefully (e.g. falls back to default viewport) rather than throwing or rendering a blank screen.
4. **`Bookmarks_BookmarksFileCorrupted_LoadsDefaults`**:
   - *Description*: Load corrupted bookmarks file.
   - *Interaction*: Overwrite `bookmarks.json` with malformed text, then load.
   - *Assertion*: System catches deserialization errors and populates the list with defaults.
5. **`Bookmarks_AddBookmarkAtExtremelyDeepZoom_PreservesHighPrecision`**:
   - *Description*: Bookmark precision check.
   - *Interaction*: Zoom to $10^{-15}$ range, save bookmark.
   - *Assertion*: Deserializing the saved JSON recovers coordinates matching the original deep coordinates.

#### Feature 6: Localization
1. **`Localization_SwitchLanguageDuringRender_DoesNotInterruptRender`**:
   - *Description*: Change language while rendering is in progress.
   - *Interaction*: Start render, immediately set `SelectedLanguage = "PL"`.
   - *Assertion*: Previous rendering continues or completes without being aborted by culture changes.
2. **`Localization_MultipleConsecutiveCultureChanges_UpdatesBindingsCorrectly`**:
   - *Description*: Rapid culture change stress test.
   - *Interaction*: Toggle `SelectedLanguage` between "EN" and "PL" ten times in rapid succession.
   - *Assertion*: No crash; property changed notifications are sent correctly; localized labels display accurately.
3. **`Localization_CultureSetterNullValue_DoesNotThrow`**:
   - *Description*: Null check on culture setter.
   - *Interaction*: Access `CurrentCulture` setter with a null argument.
   - *Assertion*: Setter ignores the input or falls back to standard, maintaining current settings.
4. **`Localization_LocalizedStatusMessage_HandlesDifferentLanguages`**:
   - *Description*: Status text format checks.
   - *Interaction*: Perform rendering in English, then in Polish.
   - *Assertion*: Status text generated after render displays correct terminology in the respective language.
5. **`Localization_InvalidLanguagePropertySet_DoesNotCrash`**:
   - *Description*: Unexpected language strings input.
   - *Interaction*: Set `SelectedLanguage = "GERMAN"`.
   - *Assertion*: Falls back to English or retains current culture without throwing an exception.

#### Feature 7: Presets / Palettes / Fractal Types
1. **`Presets_SetInvalidPaletteIndex_DoesNotCrash`**:
   - *Description*: Input palette index outside bounds.
   - *Interaction*: Dispatch generate call with `paletteId = 99`.
   - *Assertion*: Engine handles lookup safely (e.g. wraps around or falls back to first palette) without indexing crash.
2. **`Presets_SetFractalTypeToSameValue_DoesNotTriggerDoubleRender`**:
   - *Description*: Re-select current fractal type.
   - *Interaction*: Set `SelectedFractalType = FractalType.Mandelbrot` when already set.
   - *Assertion*: Property changed logic blocks execution, preventing redundant render tasks.
3. **`Presets_SwitchFractalTypeDuringActiveRender_CancelsPreviousRender`**:
   - *Description*: Switch fractal mid-draw.
   - *Interaction*: Call `GenerateFractalAsync()`, immediately change `SelectedFractalType`.
   - *Assertion*: The active `CancellationTokenSource` cancels the running task, and a new one starts.
4. **`Presets_InvalidFractalTypeCast_HandlesGracefully`**:
   - *Description*: Cast integer index outside enum range.
   - *Interaction*: Force `SelectedFractalType` to an undefined enum value.
   - *Assertion*: Rendering code falls back to Mandelbrot.
5. **`Presets_PaletteChangeDoesNotResetZoom`**:
   - *Description*: Switch colors at deep zoom.
   - *Interaction*: Zoom in, change palette.
   - *Assertion*: Color update render retains the exact zoom level.

#### Feature 8: Julia Parameter Tuning
1. **`Julia_ExtremelyLargeCoordinate_DoesNotCrash`**:
   - *Description*: Enter massive values.
   - *Interaction*: Set `JuliaReal = "1e100"`.
   - *Assertion*: Math operations (underflow/overflow) do not crash; value is parsed or clamped safely.
2. **`Julia_EmptyInputString_FallsBackToDefault`**:
   - *Description*: Empty input box.
   - *Interaction*: Set `JuliaReal = ""`.
   - *Assertion*: Falls back to the default `-0.7` coordinate.
3. **`Julia_SpecialCharactersInInput_HandlesGracefully`**:
   - *Description*: Trailing periods or symbols.
   - *Interaction*: Set `JuliaReal = "-0.7."`.
   - *Assertion*: Treated as invalid, falls back to default coordinate.
4. **`Julia_HighPrecisionInputs_ParsesToDoubleDouble`**:
   - *Description*: Match precision parameters.
   - *Interaction*: Set `JuliaReal = "-0.712345678901234567890"`.
   - *Assertion*: Coordinates match the high-precision value inside DoubleDouble parser.
5. **`Julia_TuningParametersWhileMandelbrotActive_DoesNotAffectRender`**:
   - *Description*: Coordinate editing on non-active view.
   - *Interaction*: Mandelbrot is selected, change `JuliaReal`.
   - *Assertion*: Coordinates update but Mandelbrot fractal view continues to render without incorporating Julia math.

#### Feature 9: Animation
1. **`Animation_StopMidFrame_TerminatesLoopImmediately`**:
   - *Description*: Halt loop while drawing.
   - *Interaction*: Start animation, allow a frame to start generating, then toggle animation off.
   - *Assertion*: Loop checks `IsAnimating` and terminates cleanly.
2. **`Animation_AnimationAtDeepZoomLimit_StopsOrHandlesPrecision`**:
   - *Description*: Zoom down to limits during loop.
   - *Interaction*: Run animation until coordinates range reaches precision limits.
   - *Assertion*: Precision handlers prevent system crash.
3. **`Animation_WindowResizeDuringAnimation_AdaptsFrameDimensions`**:
   - *Description*: Resize window while zoom loop runs.
   - *Interaction*: Start animation, then call `OnSizeChanged(1024, 768)`.
   - *Assertion*: Frame width/height properties update instantly, and next frame renders at `1024x768`.
4. **`Animation_SwitchFractalTypeDuringAnimation_ContinuesAnimationOnNewType`**:
   - *Description*: Switch type during play.
   - *Interaction*: Start animation on Mandelbrot, change `SelectedFractalType` to Julia.
   - *Assertion*: Loop continues running, generating Julia frames with zoom factor.
5. **`Animation_ToggleAnimationRepeatedly_DoesNotLaunchMultipleLoops`**:
   - *Description*: Rapid start/stop clicks.
   - *Interaction*: Execute `ToggleAnimationCommand` 4 times within 10ms.
   - *Assertion*: Only a single active task loop executes, avoiding thread spam.

#### Feature 10: Diagnostics Panel
1. **`Diagnostics_AdaptiveIterations_ClampedToMinIterations`**:
   - *Description*: Clamping at lower boundary.
   - *Interaction*: Force render speed to be extremely slow so ratio dictates reduction.
   - *Assertion*: Iteration limit clamps at `MinIterations` (200) and does not go lower.
2. **`Diagnostics_AdaptiveIterations_ClampedToMaxIterations`**:
   - *Description*: Clamping at upper boundary.
   - *Interaction*: Force render speed to be extremely fast (0ms).
   - *Assertion*: Iteration limit clamps at `MaxIterations` (50,000).
3. **`Diagnostics_TelemetryDisplaysZeroWhenRenderFails`**:
   - *Description*: Telemetry display on calculator crash.
   - *Interaction*: Force generator to throw an exception.
   - *Assertion*: StatusText displays error message, and stats display fallback values.
4. **`Diagnostics_AdaptiveAdjustmentWithZeroElapsedMs_DoesNotDivideByZero`**:
   - *Description*: Instant execution speed check.
   - *Interaction*: Perform render that finishes with 0ms elapsed time.
   - *Assertion*: Logic executes safely, clamping to `MaxIterations`.
5. **`Diagnostics_ToggleTelemetryPanelHotKey_SimulatesVisibilityChange`**:
   - *Description*: Shortcut toggle HUD.
   - *Interaction*: Execute key 'D' simulation.
   - *Assertion*: Telemetry HUD visibility toggles.

#### Feature 11: File Export & Clipboard
1. **`Export_SaveFileDialogCancelled_DoesNotSaveFile`**:
   - *Description*: User cancels file picker.
   - *Interaction*: Set `SaveFileDialogAction` to return `null` (cancel), call `SaveImageCommand`.
   - *Assertion*: StatusText remains unchanged; no write executes.
2. **`Export_SaveFileWriteError_DisplaysErrorMessage`**:
   - *Description*: File write throws disk error.
   - *Interaction*: Force save path to a write-protected root.
   - *Assertion*: Exception is caught; `StatusText` displays error details.
3. **`Export_ClipboardActionThrows_DisplaysErrorMessage`**:
   - *Description*: Clipboard locked by OS.
   - *Interaction*: Force clipboard copy delegate to throw.
   - *Assertion*: Exception caught; `StatusText` displays failure details.
4. **`Export_SaveFileInNonExistentFolder_CreatesFolder`**:
   - *Description*: Folder creation fallback.
   - *Interaction*: Clear `SavedImages` directory, call `SaveImageCommand` auto-save.
   - *Assertion*: Missing folder is created on the fly; file writes.
5. **`Export_SaveImageDuringActiveRender_SavesPreviousOrFailsGracefully`**:
   - *Description*: Save while rendering.
   - *Interaction*: Start render, call `SaveImageCommand` before completion.
   - *Assertion*: Saves the last successfully generated image.

---

### Tier 3: Cross-Feature Combinations (Pairwise Coverage)

1. **`Combo_ZoomAndPan_RestoresZoomLevelAndOffsets`**:
   - *Flow*: Centered zoom-in $\rightarrow$ Pan view left $\rightarrow$ Zoom out.
   - *Verify*: Zoom factor decreases, and coordinates correspond to the panned offset.
2. **`Combo_BookmarkAndZoomHistory_ClearsHistoryOnBookmarkSelect`**:
   - *Flow*: Zoom twice $\rightarrow$ Select bookmark $\rightarrow$ Inspect history.
   - *Verify*: Selecting a bookmark pushes the current view to history, meaning `CanZoomOut` remains true.
3. **`Combo_JuliaTuningAndBookmarks_SavesAndRestoresJuliaParameters`**:
   - *Flow*: Switch to Julia $\rightarrow$ Change inputs $\rightarrow$ Add bookmark $\rightarrow$ Select default Mandelbrot $\rightarrow$ Select custom bookmark.
   - *Verify*: System changes back to Julia, parses the custom coordinates, and displays them.
4. **`Combo_AnimationAndResize_AdaptsViewportDimensionsOnTheFly`**:
   - *Flow*: Start animation $\rightarrow$ Size change $\rightarrow$ Await next frame.
   - *Verify*: Next frame renders at updated aspect ratio without stopping animation.
5. **`Combo_SelectionAndReset_ClearsActiveSelectionState`**:
   - *Flow*: Start pointer selection drag $\rightarrow$ Call Reset command.
   - *Verify*: Active selection clears, `IsSelecting` becomes false, view returns to default.
6. **`Combo_LocalizationAndBookmarks_UpdatesBookmarkNamesOrDisplaysCorrectly`**:
   - *Flow*: Set language to Polish $\rightarrow$ Save custom bookmark $\rightarrow$ Switch language to English.
   - *Verify*: Bookmark entries display intact.
7. **`Combo_AdaptiveIterationsAndDeepZoom_BalancesPerformance`**:
   - *Flow*: Zoom past $10^{10}$ $\rightarrow$ Switch to CPU $\rightarrow$ Observe iteration adaptation.
   - *Verify*: System dials down iterations automatically to hit the target render budget.
8. **`Combo_SaveImageAndAnimation_SavesCurrentFrame`**:
   - *Flow*: Run animation $\rightarrow$ Trigger Save command.
   - *Verify*: Loop continues, and the frame snapshot saves.
9. **`Combo_PanByArrowKeysDuringZoomSelection_MaintainsIntegrity`**:
   - *Flow*: Start selection drag $\rightarrow$ Press Left Arrow key $\rightarrow$ Finish drag.
   - *Verify*: Active selection box stays correct; panning is isolated or ignored.
10. **`Combo_SwitchPaletteDuringAnimation_UpdatesRenderOnNextFrame`**:
   - *Flow*: Start animation $\rightarrow$ Change palette to Rainbow.
   - *Verify*: Frame loop updates colors on next frame.

---

### Tier 4: Real-world Scenarios (Realistic User Workloads)

#### Scenario 1: Mandelbrot Exploration, Custom Bookmarking, and Export
- **Steps**:
  1. Initialize view (starts at Mandelbrot default).
  2. Drag selection box over coordinate `(200, 150)` to `(400, 350)` to zoom in.
  3. Pan view down by executing `PanByPercent(0, -0.1)` (down arrow).
  4. Perform mouse wheel zoom in at cursor `(300, 300)`.
  5. Enter `"My Custom Seahorse"` into `NewBookmarkName` and execute `AddBookmarkCommand`.
  6. Execute `ResetCommand` to return to default Mandelbrot.
  7. Select `"My Custom Seahorse"` bookmark from the list.
  8. Execute `SaveImageCommand`.
- **Expected Verification**:
  - The final viewport matches the saved bookmark coordinates.
  - Custom bookmark exists in the list and bookmarks file.
  - Image is successfully saved.

#### Scenario 2: Transition from Mandelbrot to Julia with Real-time Parameter Tuning
- **Steps**:
  1. Initialize view (starts at Mandelbrot default).
  2. Select `"Julia Default"` bookmark from the presets.
  3. Verify `IsJuliaSettingsVisible` is true and settings display.
  4. Input `"0.36"` into `JuliaReal` and `"-0.1"` into `JuliaImag`.
  5. Verify that the view renders the tuned Julia shape.
  6. Pan left by dragging middle mouse from `(400, 300)` to `(450, 300)`.
  7. Toggle diagnostics HUD visibility off using the `D` key shortcut.
  8. Copy the final rendering to clipboard.
- **Expected Verification**:
  - Julia parameters parse correctly.
  - Diagnostics HUD visibility turns false.
  - Clipboard copy delegate receives a valid bitmap.

#### Scenario 3: Deep Zoom Exploration with Engine Switch and Animation Loop
- **Steps**:
  1. Initialize view.
  2. Select `"Triple Spiral Valley"` bookmark.
  3. Toggle animation zoom loop on.
  4. Wait for 5 frames to render.
  5. Toggle animation off.
  6. Verify Zoom factor exceeds $10^{10}$, causing the active engine to switch to CPU (`CPU (Parallel)`).
  7. Check that adaptive iterations have scaled down below 1,000 to keep UI responsive.
  8. Perform 3 zoom-outs using `ZoomOutCommand`.
- **Expected Verification**:
  - Active generator transitions dynamically.
  - Telemetry updates correctly.
  - History pop recovers the previous viewports.

---

## 3. Integration & Testing Implementation Plan

The test suite will be integrated into the test project as **`Fractal.Tests/UI/E2ETests.cs`**. It uses pure ViewModel integration, instantiating the real dependencies to simulate actual application flows.

### 3.1 Constructing Real Services Without Mocking
- **Calculations**: Use the real `ParallelFractalGenerator` (CPU generator) so that the mathematical render pipeline is fully exercised.
- **GPU Generator Simulation**: Since headless environments (CI) might lack GPU drivers, we can inject a mock `IFractalGenerator` that delegates calculation to the real CPU generator but overrides `Name = "GPU (Simulated)"` and `IsGpuAccelerated = true`. This lets us test engine-switching boundary conditions.
- **Zooming**: Inject the real `ZoomService` to verify the aspect ratio and viewport math.
- **Bookmarks Path Isolation**: Instantiating the real `BookmarkService` writes to `AppData`. To prevent test runs from polluting or corrupting the user's local configuration, we will write a helper method that backs up the existing `bookmarks.json`, configures the test service to write to a temporary file, and restores the backup upon test completion.

### 3.2 Simulating User Interactions
Instead of invoking full UI window automation, we call the event handlers of `MainViewModel` directly:
- **Mouse Drag / Selection**:
  ```csharp
  vm.OnPointerPressed(new Point(x1, y1));
  vm.OnPointerMoved(new Point(x2, y2));
  vm.OnPointerReleased(new Point(x2, y2));
  ```
- **Mouse Wheel**:
  ```csharp
  vm.OnMouseWheelZoom(new Point(x, y), delta);
  ```
- **Keyboard Shortcuts**: Call the corresponding command or method on the ViewModel directly:
  ```csharp
  vm.ResetCommand.Execute(null);
  vm.PanByPercent(0.1, 0);
  vm.ZoomCentered(true);
  ```
- **Size Changes**:
  ```csharp
  vm.OnSizeChanged(width, height);
  ```

### 3.3 Handling Asynchronous Rendering
Since `RequestRender()` runs `GenerateFractalAsync()` inside a fire-and-forget task scheduled on the UI context, calling `vm.OnPointerReleased` or changing a property does not block execution. To cleanly await render completion in tests without fragile `Task.Delay` calls, tests can directly call:
```csharp
await vm.GenerateFractalCommand.ExecuteAsync(null);
```
This forces the execution to run synchronously within the test execution thread, ensuring that once the call returns, the pixel buffers and HUD labels have updated.

---
