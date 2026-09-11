# Explanation workspace validation — 2026-09-08

## 2026-09-10 overlap, briefing and turbulence update

- Main implementation compiled/imported in Unity and was exercised in Play mode. A 4K Unity capture confirmed the normal HUD dock remains below the heading tape, and a full-map capture confirmed the entire dock/card relocates to the right-side gap outside the chart. Screenshots: /private/tmp/faa-brief-hud-20260910.png and /private/tmp/faa-brief-map-20260910.png (temporary local QA captures).
- A real Chart action returned cited image/telemetry/chart evidence. Its first draft exceeded the requested word budget, making the previous clipping problem reproducible. Fixed-height paging reported three pages, and advancing via the page handler reached page 2. This led to an additional bounded editorial shortening pass and stricter chart-interpretation rules; those final refinements require a post-reload live check.
- Final direct execution of NUnit assertion methods inside Unity (not a Unity Test Runner report) passed **80/80**: 47 core, 9 panel, 11 turbulence-mode and 13 existing range/gain cases. The pure runner also passed 47/47. The direct runner is Tools/ExplanationVerification/RunEditorAssertions.cs and creates/removes only its temporary test UI.
- Turbulence diagnosis: the live Unity snapshot had no region/turbulence samples. The existing calm-rain simulator helper explicitly sets all 13 regional values to zero; it was not run or modified for this request. The renderer previously drew rain irrespective of TURB mode. TURB now suppresses rain; WX+T labels the absent turbulence scan. Missing/partial/nonfinite regional samples are unavailable, not zero. No fabricated magenta cells and no simulator flight/weather changes.
- Final reload completed with no compilation errors, and a live Chart action exercised the shortening pass successfully: three read-only tool calls, a cited single-page Picture / Context / Limit answer, and explicit abstention on unreadable image labels. A new 4K capture (/private/tmp/faa-brief-map-controls-final.png) shows the expanded 5 NM chart, upper chart toolbar, left traffic controls and right-side brief together. Programmatic bounds checks confirmed no overlap with the chart, ActionPanel or TrafficControlStrip.
- Computer Use reported the Mac locked, but Unity CLI remained usable for the final reload, native Unity capture and actual Button.onClick handler checks. Physical pointer interaction was not independently verified; the UI actions were invoked through their existing runtime handlers. No simulator aircraft or weather conditions were changed.
- A final live HUD capture (/private/tmp/faa-turb-mode-final.png) confirmed TURB hides the texture and displays TURB SCAN UNAVAILABLE with No turbulence samples · not a clear indication, while the source line no longer calls the turbulence channel DATA LIVE. The main telemetry feed was fresh for this check.

Earlier validation entries below are historical.

## Click-only redesign update

- Replaced the typed-question drawer with a 520 × 82 lower-center action dock and a 520 × 196 brief/detail card, at the 1920 × 1080 canvas reference scale. Four fixed actions: Traffic, Weather, Chart, Status. No input field or wide-workspace mode remains.
- Unity's latest recompile_status reported completed, failed: false, errors: [] after importing the new UI, action catalog, prompt and regression tests.
- **40/40 pure checks passed** using the GUI-independent runner, including the new action allowlist and short-brief policy checks.
- Added eight Unity UI fixture cases for default/no-input/no-send behavior, geometry and canvas order, four screen-size cases, hide/restore/details, and unknown-action rejection. The interactive run started but has **not returned results**; do not count these cases as passed.
- Computer Use reported the Mac locked during visual verification. A subsequent read-only Unity main-thread operation timed out. The new runtime layout, pointer interactions and live one-tap brief therefore remain **visually / interactively unverified** until the Mac is unlocked and any waiting Unity prompt is resolved.
- No aircraft/SSH changes, credential changes, commit or push were made for this redesign.

The entries below describe earlier backend validation, before this UI redesign.

## Passed

- Provider inventory and streaming function-call compatibility at `https://subtoken.shop/v1`, using `subtoken-sonnet-4-6`.
- Full two-round live provider smoke test: HTTP 200 for both rounds, two read-only tool results, 202 SSE events, historical chart-image attachment, and only returned evidence IDs cited in the final answer. The model distinguished display visibility from stale data and unknown power, and identified the historical screenshot's large map-disc geometry. This was a protocol/vision test, **not a live-flight validation**.
- **34/34 pure checks** executed against the same NUnit assertion source used by `FAA.Explanations.EditorTests`, using the GUI-independent .NET runner.
- Unity initially imported/compiled the new module successfully. After subsequent refinements, a separate compiler verification used the running editor's exact response files, Unity 6000.5.10f1 SDK, references and analyzers to compile updated `FAA.Explanations.Core`, `TrafficRadar` and `Assembly-CSharp` into a temporary directory. All three succeeded. The new assistant sources generated **zero errors and zero warnings**; existing project warnings remain.
- A Unity CLI snapshot call resolved all seven evidence records in Editor preview; telemetry stayed unavailable/preview instead of becoming made-up live values, procedural chart fallback was identified, and the existing visual-analysis manager was found with no cached findings.
- Scoped source scan found no API-key-shaped literal in the new module or verification tooling. The key is not serialized or checked into source.
- Scoped `git diff --check` passed.

The provider injects a boolean `_` field into function arguments. Initial live tests correctly rejected it and flagged the resulting fabricated citation IDs. The schema now explicitly permits this **ignored compatibility no-op only when boolean**. Other extra keys, paths, URLs and commands remain blocked. Missing/unresolved citations now produce a distinct UNVERIFIED response state rather than a normal Complete status.

## Still required in the unlocked editor

Computer Use reported that the Mac was locked. Unity's interactive EditMode run remained waiting and main-thread CLI operations timed out. Do **not** count that Unity test run as passed.

1. Unlock the Mac and resolve any waiting Unity scene prompt. Refresh/import the latest sources (including the separate glyph and citation-link scripts); Unity generates their metadata.
2. Run `FAA.Explanations.EditorTests` and the existing `FAA.Customization.EditorTests` in the editor.
3. Enter Play mode in ExperimentScene. Verify the four-action PILOT BRIEF dock appears without any automatic network request.
4. Check the compact card between the radars below the heading tape at the actual Game-view scale; hide/restore, Escape, source-link navigation, detail scrolling, reduced motion, image-sharing OFF, stop/retry and reopening a running/completed brief. Confirm there is no input field, and outside-HUD controls remain usable.
5. Tap Status and Chart. Confirm their short streamed answers, source values/ages and captured image crop match the displayed systems; test switching actions, missing/stale data and provider failure presentation. Do not treat a completed brief as live flight guidance.

No commit or push was made for this request. Unrelated existing worktree changes were preserved.
