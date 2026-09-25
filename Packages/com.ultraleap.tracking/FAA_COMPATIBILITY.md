# FAA local compatibility patch

Upstream: Ultraleap Tracking 7.3.0, Apache-2.0. Embedded through Unity Package Manager.

Unity 6000.5 reports two removed `Object.GetInstanceID()` calls in `Core/Editor/Scripts/EditorUtils.cs` as compile errors. The reference-replacement helper now compares Unity object references directly (`refValue == a`) instead of extracting and comparing native integer IDs. This also avoids dereferencing a null reference.

No tracking algorithms, coordinate conversions, native plugins, assets or licenses were changed. Preserve or re-evaluate this focused patch when upgrading the embedded package. FAA hand input and offset configuration live in the project's `FaaWorkspaceGestureInput`, not in the vendor sources.
