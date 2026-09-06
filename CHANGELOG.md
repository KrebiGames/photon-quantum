# 3.1.0

## Preview

Disclaimer: The Quantum SDK 3.1.0 development snapshots are not intended to be used for live games.

**Breaking Changes**

- The library `Quantum.Deterministic.dll` was merged with `Quantun.Engine.dll` and remnants of the old dll have to be deleted from projects that migrate to SDK 3.1
- The API for sending and processing commands has changed so that multiple commands per frame can be send without requiring extra management using a `CompoundCommand`. Sending commands changes to `QuantumGame.AddCommand(DeterministicCommand)` and processing commands uses an iterator `foreach (var command in frame.GetPlayerCommands<MyCommand>(player)) { }`.
- The `DeterministicSessionConfig` inspector now computes the `Hard Tolerance` based on the simulation rate and input offset ping start, to override this behaviour toggle `Override Hard Tolerance`
- `SessionRunner.Arguments` now requires setting an explicit `TaskRunner` to be set. This can be done by either specifying `TaskRunner = QuantumTaskRunnerJobs.GetInstance()` or by implicitly setting the TaskRunner by using `GameParameters = QuantumRunnerUnityFactory.CreateGameParameters` for Unity. Use `TaskRunner = new DotNetTaskRunner()` outside of Unity
- The demo input `.unitypackage` now requires the installation of the Unity module `com.unity.inputsystem`
- `Quantum.Profiling.HostProfiler` was renamed to `Quantum.HostProfiler`
- The `DeterministicSessionConfig` inspector now computes the `Hard Tolerance` based on the `Simulation Rate`, input `Offset Ping Start` and input `Offset Min`, to override this behaviour toggle `Override Hard Tolerance`

**What's New**

- Added `table` components, which are an alternative to the sparse set ECS components that scale better, see the documentation for more details
- Added a new physics solver `ProjectedGaussSeidel` that greatly improves the stability when stacking rigid bodies, to use the legacy solver select it in the SimulationConfig
- Added `SimulatorContext` to exposes simulation callbacks, which allow for modifications to future simulation and prediction advancements, see the documentation for more details
- Optimized the memory required to store the sparse set ECS components which increases the performance of frame copies
- Upgraded the graph profilers to combine multiple metrics in one graph to better analyse general simulation performance and online play
- Added Unity profiler counters such as `Q Frames Verified`, `Q Frames Predicted`, and `Q Simulation Time`, that can be added to the Unity profiler windows as a custom profiler module
- Added `FPVector2` and `FPVector3.Average` methods for computing the average of a span of vectors
- Added new non-biased random number generation methods to `RNGSession`: `Next(long, long)`, `NextInclusive(long, long)`, `Next(uint)`, `Next(ulong)`, `NextUInt32()`, `Int64()`
- Added `QuantumIgnore` label support - apply to an asset to be ignored by `QuantumUnityDB`, useful on prefabs that are used as map prototypes exclusively for example
- Added `SimulationUpdateTime.EngineUnscaledCappedDeltaTime` which uses a capped version of unscaled delta time, this improves support for breakpoints and Unity Editor pausing in local mode
- Added an `FP` converter utility window that converts `double` to `FP` and raw values
- Added a new configurable value called `Heuristic Weight` to the `NavMeshAgentConfig` which can improve the performance of the A* algorithm algorithm, try setting it to `1.5`
- Added a NavMesh import option to load auto-generated navmesh links directly from the Unity navmesh, only works in Unity Editor
- Added QuantumRunnerExtensions methods to simplify starting Quantum in different modes by providing specific `Init()`-methods to create `SessionRunner.Arguments`, see the usage inside the `QuantumRunnerLocalDebug.cs` script for example
- Added support for Unity's `InputSystem` actions which is now used by the demo Quantum input scripts
- Added a 3D Physics standalone solver API, available through the `Quantum.Physics3D.CollisionSolver` static class
- Added `PhysicsBody3D.ComputeWorldInertiaTensorInverse` to compute the world-space version of a physics body inverse inertia tensor
- Added support for scheduling multiple Physics Updates in the same frame simulation
- Added Asset Bundle support - Quantum Unity DB recognizes assets that can be loaded by Asset Bundles. Use `QuantumAssetSourceAssetBundle` static delegates to change the way bundles themselves are loaded and unloaded
- Added `Quantum.Compression` - an abstract base class for compression algorithms. Comes with two implementations: `CompressionDotNet` (default) and `CompressionSharpLibZib` (enabled when `com.unity.sharp-zip-lib` package is present). Using the latter might help with "Runtime Speed with LTO" Web builds issues. Both implementations produce the same results and rely on `GZip` format

**Changes**

- The default simulation rate was increased from `60 Hz` to `64 Hz` (using powers of two), ensuring that `DeltaTime` has no rounding error and providing greater precision in physics calculations
- The multi client scripts have been moved out of the SDK package into a `.unitypackage` (`Assets/Photon/Quantum/PackageResources/Quantum-MultiClient`)
- Exporting replays and snapshots via the menu now saves the last save location as a relative path
- Changed the NavMesh API by renaming `Map.NavMeshLinks` to `Map.NavMeshAssets` and by removing the property `NavMeshAgentConfig.AutomaticTargetCorrection` instead `AutomaticTargetCorrectionRadius` > 0 is checked to test if target correction is enabled
- Renamed `QuantumGame.CreateSavegame()` to `GetSnapshotFile()`, retired the `QuantumRunnerLocalSavegame.cs` script and merged its functionality with `QuantumRunnerLocalDebug.cs`
- `ByteUtils` compression methods made obsolete, use `Quantum.Compression` instead
- Stats on the QuantumStats window (like bandwidth) are now smoothed and show an average (1 second) to make them more readable
- `QuantumUnityDB` does not throw exceptions in `TryGet*` methods if the DB was failed to be loaded
- `QuantumCallbackHandler_UnityCallbacks.LoadAddressableScenePathsAsync` is now static

### Build 2200 (Sep 03, 2026)

**What's New**

- `SimulatorContext.MaxPredictedTicks`, a way to clamp the number of predicted frames during simulator callbacks
- New `SimulatorContext` API: `IsRollbackRequired`, `WillRollbackPrediction`, `HasRolledBackPrediction` and `LatestVerifiedRolledBackTo`. Check the API documentation for further details

**Changes**

- `SimulatorContext.TargetPredictedTick` now sets `MaxPredictedTicks` in order to reach the targeted frame according to current `PredictedFromTick`
- `SimulatorContext.RollbackPrediction` was replaced by `SkipPredictionRollbackIfPossible`, which has the opposite semantics, defaults to `false` and makes it clearer that the skipping rollback is a request. The old property is obsolete and forwards to the new one
- `SimulatorContext.TicksToPredict` is now limited by the session rollback window and reports zero while the session is stalling, so the pending tick counts no longer describe work that will not be performed

**Bug Fixes**

- Fixed: An issue that could cause `CallbackSimulationStageFinished` to not be called when the simulation is stalling, even though `CallbackBeforeSimulationStage` is called before predictions
- Fixed: An issue that could cause the simulator to hang indefinitely when skipping a prediction rollback via `SimulatorContext.RollbackPrediction` in a session Update where the latest Verified state simulated past the previous Update Predicted tick

### Build 2199 (Sep 01, 2026)

**What's New**

- Adding `FrameCopyTime` and `FrameCopies` to simulation stats

**Changes**

- NavMeshPathfinder stores typed asset refs; new Config/NavMesh properties replace obsoleted ConfigId/NavMeshGuid
- Use var for locals with inferable asset types
- Removed remaining explicit asset type names where inferable from typed refs
- Removed redundant type arguments from FindAsset calls with typed asset refs
- Replaced GetAsset plus null-check patterns with TryGetAsset
- Added typed GetAsset(AssetRef<T>) resource manager extension and removed now-redundant casts at call sites

### Build 2197 (Aug 29, 2026)

**What's New**

- `QuantumJsonSerializer` `typeResolver` constructor parameter. Allows for custom type resolution in standalone runners

**Improvements**

- Improved component filter/lookup performance

**Changes**

- Reworked (opt-in) copy-on-write mechanics to reduce overhead
- Improved loading performance of the `FrameDiffer` GUI

**Bug Fixes**

- Fixed: An error that causes a crash when all joints of a joint component are removed during a physics callback
- Fixed: An error that causes a crash when all joints of a joint component are removed during a physics callback
- Fixed: An issue where disposing a DynamicMap created with `FromStaticMap` freed 2D polygon buffers still used by the source map
- Fixed: An issue that caused no map entities to be created when switching to DynamicMap that was cloned from a static Map with map entities
- Fixed: An internal overflow in 2D and 3D HitCollection sorting that could cause wrong hits to be perceived as the closest one
- Fixed: `UnityJsonUtilityConvert` no longer throws an exception on deserialization if a `[SerializeReference]` object fails have its type/instance created, but remains unreferenced. This enables Unity-only types and Unity-only fields

### Build 2191 (Aug 26, 2026)

**Bug Fixes**

- Fixed: An issue on 3D Capsule shape prototypes baked from Unity source colliders potentially taking the radius from the wrong axis when the source GameObject had negative scale
- Fixed: An issue on 2D and 3D Compound shape prototypes having their sub-shapes offset and size scaled incorrectly when the prototype GameObject had negative or non-uniform scale
- Fixed: An issue on 2D and 3D Box shape prototypes baked from Unity source colliders with negative scale having their extents clamped to zero
- Fixed: An issue with 2D and 3D Shape prototypes diverging from their Unity source collider when the prototype GameObject is scaled

### Build 2189 (Aug 24, 2026)

**Breaking Changes**

- Frame `DeferredAdd` and `DeferredSet` overloads were removed, as well as `CommitDeferred`. Adding table components to an entity while a filter is iterating that entity's current table no longer throws an exception, so deferring component adds is no longer necessary

**What's New**

- `SimulationConfig.Entities.FrameCopyMode` setting can be used to enable (opt-in) copy-on-write mechanics, replacing a full frame copy before predictions with on-demand block copying on write access

**Changes**

- An Entity that subscribes to collision callbacks from inside a physics callback now starts receiving them on the next tick instead of the same tick
- Improved `FrameDiffer` load performance

**Bug Fixes**

- Fixed: An issue where the untyped `FrameBase.AddOrGet(EntityRef, int, out void*)` would always add/overwrite

### Build 2185 (Aug 20, 2026)

**Bug Fixes**

- Fixed: An issue that computed the wrong position for the `Hinge Joint` when multiple joints are connected

### Build 2183 (Aug 19, 2026)

**Changes**

- `HitCollection` and `HitCollection3D` now support initial capacity 0
- Improved the deserialization performance of `QuantumJsonSerializer`
- Physics `Hit Collection Items` capacity is now clamped to a minimum of 4, and allocating a hit collection with a non-positive capacity throws an `ArgumentOutOfRangeException`
- Physics broad-phase queries added after the start of the Physics system now no longer produce a valid `PhysicsQueryRef` and are effectively a no-op
- Updated Photon Realtime to version `5.1.18`

**Bug Fixes**

- Fixed: An issue that prevented a body from sleeping when it touched sleeping bodies or was connected to them by joints, when the contact generated no force, such as a box spawned next to a stack
- Fixed: An issue that caused bodies connected by joints to move even when they were sleeping
- Fixed: `HitCollection` and `HitCollection3D` causing Memory Integrity Check failure in 32-bit platforms
- Fixed: An issue with 2D and 3D broad-phase shape overlap and shape cast queries with a Compound shape that could cause released native memory to be accessed
- Fixed: An issue in 3D physics that could cause a crash or bogus hits when a mutable mesh collider was disabled or removed during a physics callback and injected broad-phase queries were checking that same mesh
- Fixed: An issue that could cause memory corruption when adding broad-phase queries during the physics update (e.g. from collision callbacks)

### Build 2181 (Aug 18, 2026)

**Bug Fixes**

- Fixed: An issue that caused some of the `QuantumDotnetBuildSettings` buttons to not work with new solution extension `slnx` very well

### Build 2180 (Aug 15, 2026)

**What's New**

- Allow overriding the properties of the limbs, head, chest, and hips that are baked and managed by the QuantumRagdoll to create asymmetric ragdolls
- Gizmo handles for editing the `QuantumRagdoll` and allowing it to automatically bake the ragdoll fields using an `Animator` component reference

**Changes**

- Updated Photon Realtime to version `5.1.18`

### Build 2176 (Aug 13, 2026)

**What's New**

- `QuantumRagdoll` unity component now accepts a PhysicsMaterial to be assigned to all limb colliders

**Bug Fixes**

- Fixed: Assertion errors in Debug when adding/removing components in a project that has more than 255 components defined
- Fixed: An issue with Physics Joints that could cause clients to desync when the solver Warm Start was enabled

### Build 2175 (Aug 12, 2026)

**Bug Fixes**

- Fixed: An issue with snapshot view interpolation mode that caused out of bounds tick requests to warp instead of being clamped
- Fixed: An issue that caused keeping the snapshot recording enabled for destroyed entities that flagged for `ManualDisposal`

### Build 2171 (Aug 06, 2026)

**Bug Fixes**

- Fixed: Regression (introduced on build 2148) that would cause `KeyNotFoundException` on builds when subscribing event listeners

### Build 2167 (Aug 05, 2026)

**What's New**

- New settings for the Quantum Ragdoll component that allow to setup the chest and hips sizes and offsets

**Bug Fixes**

- Fixed: [CodeGen] only emit namespaces to modules that declare them

### Build 2157 (Aug 04, 2026)

**Bug Fixes**

- Fixed: A regression that made the `sharedResourceManager` on server simulation to be accidentally discarded with rooms

### Build 2148 (Jul 25, 2026)

**Breaking Changes**

- [CodeGen] multiple signals with the same name are now an error

**What's New**

- [CodeGen] `#pragma module <name>`: adding the pragma to a qtn file will emit the output (components, prototypes, events etc.) to a set of module-specific .cs files. This can be used to break apart the "core" module, if it becomes too unwieldy
- [CodeGen] support for access modifiers for fields and types. Public is still the default

**Changes**

- [CodeGen] code gen output for `FrameEvents`, `FrameSignals` and `Statics` has been changed to make support for `modules` possible

### Build 2142 (Jul 17, 2026)

**Breaking Changes**

- Photon enterprise cloud users require their server to upgrade to Quantum protocol version `3.1.0.0`

**What's New**

- Added a new Quantum callback `CallbackGameResultResponse` to react to completed game result operations

### Build 2139 (Jul 14, 2026)

**Bug Fixes**

- Fixed: An issue in 2D Physics when removing a `PhysicsCallbacks2D` component from an entity during collision callbacks, which could cause such entity to use the wrong callback flags on that frame

### Build 2134 (Jul 09, 2026)

**Changes**

- `BakeCacheRoot` is now available as editor setting (needs `QUANTUM_ENABLE_QMAP`)

**Bug Fixes**

- Fixed: Invalid check in `QuantumEditorAutoBaker` when using Build auto-bake triggers

### Build 2133 (Jul 08, 2026)

**What's New**

- Quantum state inspector now shows if a component on an entity is a table component

**Bug Fixes**

- Fixed: AssetBundle builds occasionally not being detected if `Build` auto-bake trigger is used

### Build 2132 (Jul 07, 2026)

**Bug Fixes**

- Fixed: `NullReferenceException` when setting up a new Quantum scene

### Build 2130 (Jul 06, 2026)

**Bug Fixes**

- Fixed: The Quantum icon is rendered for the gizmo overlay again for Unity version 2022.3+
- Fixed: Issues with false navmesh border generation, enable QuantumNavMesh.ImportSettings.RepairSeams on the navmesh import script

### Build 2128 (Jul 03, 2026)

**Bug Fixes**

- Fixed: Baking `qmap` immediately imports file in BakeCache if they've just been created

### Build 2127 (Jul 02, 2026)

**Bug Fixes**

- Fixed: An issue in the 2D navmesh agent internal steering that caused right and left steering to use a slightly different rotation speed
- Fixed: An issue where navmesh agents that failed their search can influence other agent waypoint detection

### Build 2124 (Jul 01, 2026)

**Changes**

- `QuantumStaticColliderSettings` fields in concrete static collider components were moved to their respective base class `QuantumStaticCollider2D/3DSource`
- `QuantumStaticColliderSettings.Asset` was renamed to `.UserAsset`

**Bug Fixes**

- Fixed: Regression introduced in Build 2076 that would cause static primitive colliders to scale their position offset with abs scale instead of signed

### Build 2119 (Jun 30, 2026)

**Changes**

- JsonUtilityExtensions.InstanceIDHandlerDelegate now uses `EntityId` for Unity 6000.3+

**Bug Fixes**

- Fixed: A regression that caused `Draw.Shape()` to not draw gizmos in the editor

### Build 2118 (Jun 29, 2026)

**Bug Fixes**

- Fixed: Issue when baking static mesh colliders with N degenerate triangles causing the last N triangles to be dropped from the baked mesh
- Fixed: An issue that caused the Unity navmesh data to be saved on the Unity scene instead of on a separate asset during map baking
- Fixed: An issue that caused the imported Asteroids sample to have broken materials for Unity 6.5+

### Build 2112 (Jun 26, 2026)

**Improvements**

- Warn messages logged when degenerate triangles are found during `QuantumStaticMeshCollider3D` baking now include the name of the GameObject

### Build 2110 (Jun 24, 2026)

**What's New**

- New `CharacterJoint3D` type, useful for creating ragdolls and ball-and-socket constraints. Configurable via `PhysicsJoint3D` component prototype
- `QuantumRagdoll` MonoBehavior: a wizard-like component to help creating ragdolls and updating their view elements based on simulation state
- A template ragdoll can now be created by right-clicking in editor Hierarchy > Quantum > 3D > Ragdoll Entity

**Changes**

- `FPQuaternion.FromToRotation` changed how it disambiguates rotation of opposite from/to vectors, now matching Unity 6.5+ standard
- Restored `Bake All` button for `QuantumMapData` using `qmap`

**Bug Fixes**

- Fixed: DivisionByZero exception in `FPQuaternion.RotateTowards` when quaternions are almost identical or non-normalized AND `maxDegreesDelta` is negative
- Fixed: An issue with 3D capsule-triangle collision detection that was causing false-negatives in certain conditions
- Fixed: Memory leak when using Trace Allocations as Frame Heap tracking mode

### Build 2104 (Jun 19, 2026)

**Changes**

- The instant replay script now reuses its runner for consecutive replays

**Bug Fixes**

- Fixed: An issue with the `QuantumRunnerRegistry`  that allowed adding the same runner multiple times

### Build 2099 (Jun 17, 2026)

**Bug Fixes**

- Fixed: `QuantumCodeGenSettings` migration from partial type using invalid `ViewOutputPath`

### Build 2096 (Jun 17, 2026)

**What's New**

- TerrainCollider asset is now optional. Leaving a `QuantumStaticTerrainCollider3D.Asset` field empty will make the terrain data be baked directly and only to the map mesh data, without saving an intermediary FP-based heightmap on the asset as before
- QuantumHeightMap is a new class (not an AssetObject) that provides basic FP-based heightmap storage and operations, which used to be covered only by a TerrainCollider asset
- Terrain colliders can now be automatically baked along with map colliders and prototypes, configurable in Quantum Editor Settings

**Changes**

- Updating Third Party Notices

### Build 2095 (Jun 16, 2026)

**Changes**

- Upgrading Photon Realtime to version `5.1.15`

**Bug Fixes**

- Fixed: Compile errors if `com.unity.modules.assetbundle` is not used - a version define `QUANTUM_ENABLE_ASSET_BUNDLE_ASSET_SOURCE` was added to relevant asmdefs
- Fixed: Regression in 2D and 3D Physics callbacks that could cause desyncs due to OnEnter callbacks being called in late-joiners
- Fixed: An issue that caused stepping the editor in paused play mode to not play back exactly one Quantum tick

### Build 2089 (Jun 12, 2026)

**What's New**

- `QuantumCodeGenQtnSettings` - contains all the settings used for Qtn codegen. Editable in Project Settings window
- `QuantumEditorSettings.SdkRoot`
- `QuantumEditorSettings.GetSdkPath(string relative)`

**Changes**

- `QuantumCodeGenSettings` is now obsolete
- `AssetGuidOverrideDependency` dependency is updated on saving `QuantumEditorSettings`
- Overrides live outside the AssetDatabase now, so saving them refreshes the override dependency on its own, separately from the asset hash dependency - two import waves instead of one coalesced
- `QuantumUnityEditorPaths` is now obsolete

**Bug Fixes**

- Fixed: An issue that would allow a Unity `TerrainCollider` to be assigned to a `QuantumStaticTerrainCollider3D.Asset` field on inspector
- Fixed: OnSettingsGUI override access modifier across assemblies
- Fixed: Base lives in Quantum.Unity.Editor.CodeGen, override in
- Fixed: Quantum.Unity.Editor; for a protected-internal base member the
- Fixed: Cross-assembly override must drop the internal portion
- Fixed: Rebuild AssetGuidOverrides dictionary after JSON load
- Fixed: JsonUtility.FromJsonOverwrite does not re-fire OnEnable, so the
- Fixed: _assetIdToAssetGuidOverride dictionary stayed empty after Load
- Fixed: Breaking TryGetAssetGuidOverride/SetGuidOverride lookups
- Fixed: Implements ISerializationCallbackReceiver to rebuild on deserialize

### Build 2085 (Jun 06, 2026)

**What's New**

- CodeGen is now validated by having the dll's MVID read directly and compared against loaded version

**Changes**

- Define `QUANTUM_DISABLE_CODEGEN_MVID_CHECK` define to disable the new `ModuleVersionId` check CodeGen is now performing to make sure the latest version has been loaded
- Removed soon to be deprecated `DEVELOPMENT_BUILD` define

### Build 2082 (Jun 03, 2026)

**Changes**

- CodeGen: components marked with `[CodeGen(PartialMonoBehaviour)]` (or legacy `[CodeGen(NotMainUnityWrapper)]`) will now have their corresponding partial `MonoBehaviours` generated to a single file `Quantum.CodeGen.UnityPartialMonoBehaviours.cs` (was: `.Partial.cs` per each marked component`)
- `[CodeGen(NoUnityPrototypeWrapper)]` and `[CodeGen(NotMainUnityWrapper)]` are now obsolete, use `[CodeGen(NoMonoBehaviour)]` or `[CodeGen(PartialMonoBehaviour)]`, respectively
- Quantum local UPM conversion now places the packages into the Packages folder

**Bug Fixes**

- Fixed: Obsoletion warnings for `ProjectWindowUtil.CreateAssetWithContent`

### Build 2081 (Jun 02, 2026)

**Changes**

- Upgrading Photon Realtime to version `5.1.14`

**Bug Fixes**

- Fixed: The `LineIntersectsAABB` static function was returning the wrong penetration when the points weren't inside the AABB object
- Fixed: The `LineIntersectsAABB_SAT` static function could return false positives given specific lines
- Fixed: An issue that could cause IL2CPP builds to fail with `MSVC C2664`

### Build 2077 (May 29, 2026)

**What's New**

- Static Terrain collider can now down-sample Unity source terrain during baking using box-filter average
- Quantum HUB popup can be disabled by set the define `QUANTUM_DISABLE_HUB_POPUP`
- `MapDataBakeCallbacks.OnCollectColliders2D/3D`: is called during colliders baking with the current list of colliders found on a scene. Colliders can be added to the list or removed from it (e.g. playable area pruning)
- Custom inspector for `QuantumMapBakedDataImporter`
- `QuantumMapData.MapPrototypeReferences`
- `NavMeshMapSettings` — decouples navmesh baking from the Map instance
- `QuantumNavMeshCollection`
- `PhysicsMaterial` support on `QuantumStaticTerrainCollider3D`
- `QuantumAssetObjectScriptedImporterAttribute`

**Changes**

- The gizmo toolbar header toggle does not change each gizmo state anymore
- `QuantumUnityDB.ScopeContext` now appends AssetBundle variant to the output bundle name, if present
- Only enabled gizmo shape rendering with `QUANTUM_DRAW_SHAPES` in `DEVELOPMENT_BUILD`
- QTerrain now stores guid inside; this makes it easier to create such assets on demand and works well with failing-to-deserialize-references-on-bootstrap bug."
- This reverts commit dc45791c6cdfbc831663e148d987108fa01189bb
- QTerrain now stores guid inside; this makes it easier to create such assets on demand and works well with failing-to-deserialize-references-on-bootstrap bug
- Made `QuantumMapData` partial, allowing it to be extended more easily

**Bug Fixes**

- Fixed: Quantum gizmos rendering for URP multi-camera support
- Fixed: An issue that causes an exception in gizmo rendering when map colliders game objects are deleted at runtime
- Fixed: Compile errors (GUID is defined in UnityEngine in some Unity versions...)
- Fixed: `ApplyAndImport` obsolete warning in Unity 6000+
- Fixed: Inactive GameObjects are now ignored during baking
- Fixed: `GridY` was assigned an incorrect value during navmesh import
- Fixed: Terrain static collider reverted to direct `TerrainData` ref (asset ref would introduce an indirect qunitydb dependency and broke baking)

### Build 2069 (May 27, 2026)

**What's New**

- Added an experimental view interpolation mode that is extremly light-weight and stable: `ExponentialDecay`, when using only this interpolation mode the performance can be boosted by toggling on `DisableInterpolatableStates` to disable two entire frame copies

**Changes**

- Upgrading `Photon Realtime` to version `5.1.13` (preview)
- Implementing Unity Auditor criticial feedback

**Bug Fixes**

- Fixed: Assert exception in TriangleMesh when skipping mutable metadata serialization

### Build 2065 (May 20, 2026)

**Changes**

- The export replay menu options now have more meaningful descriptions

**Bug Fixes**

- Fixed: A memory leak in table metadata

### Build 2062 (May 19, 2026)

**What's New**

- `TriangleMesh.SerializeMutableData` property now allows mutable triangle data to be skipped during serialization.  
Useful to reduce snapshot size if either the mutable data is known to not have been modified or the modifications can be replicated on de-serialization exactly as they happened on the snaptshot provider
- `QuantumEntityPrototypeAssetObjectImporter.EnableNestedPrototypes` (`true` by default)

**Changes**

- Optimizig `QuantumStats` by removing garbage creation and running in lower frequency
- Optimizing `QuantumMeshCollection` by removing the read/write flag

**Bug Fixes**

- Fixed: Invalid "Scripted-importer asset" warning during prefab importing
- Fixed: Memory leaks in 2D and 3D Physics systems and Map runtime collider buffers
- Fixed: `QuantumAsset` label being removed for prototypes that failed to import
- Fixed: Error when a prefab without `QuantumEntityView` in the root had a nested prototype with a view

### Build 2054 (May 12, 2026)

**Bug Fixes**

- Fixed: An issue that caused exceptions (e.g. from gizmo rendering) on invalid or broken asset refs in `Map.NavMeshLinks`

### Build 2053 (May 09, 2026)

**What's New**

- Support to using multiple Unity colliders as source for a Quantum Compound Shape prototype
- Support to using 3D Unity Capsule as source for a 2D Quantum Capsule shape in inspector

**Bug Fixes**

- Fixed: An issue where component filters involving both kinds of components didn't yield entities (was caused by a mistake in preview build 2005)

### Build 2047 (Apr 28, 2026)

**Bug Fixes**

- Fixed: An issue where some IL2CPP builds crash when frame heaps are disposed

### Build 2045 (Apr 25, 2026)

**Bug Fixes**

- Fixed: `HitCollection` and `HitCollection3D` causing Memory Integrity Check failure in 32-bit platforms

### Build 2044 (Apr 24, 2026)

**Bug Fixes**

- Fixed: Using default output path in `[CodeGen(UnityWrapperFolder, ...)]` causing file to be deleted immediately after being generated

### Build 2042 (Apr 21, 2026)

**Bug Fixes**

- Fixed: An issue that caused scene view components to be stale after reusing the `QuantumEntityViewUpdater`, now `Activate()` is called on them when starting the new game
- Fixed: An issue with the LUT generation menu entry that tried to generate the files into a non-existing folder
- Fixed: An issue that code generated union structs that could potentially cause a desync in `GetHashCode()`

### Build 2041 (Apr 18, 2026)

**What's New**

- `FPMath.Sort` API for spans and buffers with either a comparer delegate or based on a comparable field

**Bug Fixes**

- Fixed: An issue in the `UnityNavMeshAreaDrawer` (`UnityNavMeshAreaAttribute`) that could cause storing wrong areas

### Build 2040 (Apr 17, 2026)

**What's New**

- Added `Frame.SystemExists<T>()` to check if an exising system type was created at simulation start, all other enabled queries now log a warning instead of an error when the system does not exist

**Bug Fixes**

- Fixed: An issue with QuantumStartUI animation regression

### Build 2039 (Apr 16, 2026)

**Breaking Changes**

- Removing support for global Unity navmesh bake (not for `com.unity.ai.navigation`)

**What's New**

- Added a virtual `Authenticate()` method to the `QuantumStartUIConnection` class to quickly enable custom authentication

**Removed**

- Removed the `SerializableEnterRoomArgs` class from the SDK

### Build 2038 (Apr 15, 2026)

**What's New**

- Possibility to set automatic importing of physics layer list and matrix from Unity
- Possibility to import Unity physics layer list without importing the matrix

### Build 2036 (Apr 11, 2026)

**What's New**

- `QuantumUnityDBScope.Entries` list
- Added checking for pooled game objects using `QuantumEntityViewPool.IsBorrowed`

**Changes**

- Changed the signature of UpdateManagedReferenceIds() to make it less specific, Editor baking in general will change soon

### Build 2029 (Apr 08, 2026)

**Removed**

- Deleted the script from `Assets/Photon/Quantum/Simulation/Core/Addons.T4.tt`, it should not have been added to the SDK, please remove this file manually

### Build 2026 (Apr 03, 2026)

**What's New**

- Added a Unity gizmo util method to draw cylinders `GizmoUtils.DrawCylinder`

### Build 2024 (Apr 02, 2026)

**What's New**

- The Quantum gizmo toolbar gizmo setting hides unsused module scopes (e.g. physics, navmesh), or can deactivated with a custom define `QUANTUM_DISABLE_PHYSCS_GIZMOS`, `QUANTUM_DISABLE_NAVMESH_GIZMOS`
- Added a way to customize the Quantum Gizmo toolbar popups see `CreateStylePopupContent()`

**Changes**

- Updated Photon Realtime SDK to version  `5.1.12`

**Bug Fixes**

- Fixed: `ComponentPrototypeSet` not being sorted properly
- Fixed: Unity 6000.4 warnings

### Build 2011 (Mar 25, 2026)

**What's New**

- Compound prototypes. Quantum prefabs can now also have nested `QuantumEntityPrototype` components and will be baked as compound prototypes, that is they will have `EntityPrototype.Nested` array set accordingly. When compound `EntityPrototype` is materialized, the root entity will have `EntityGroup` component added to tie the lifetime of nested entities to their root
- `EntityGroup` - a built-in component that binds lifetime of entities to their owner
- `QUANTUM_DISABLE_PROTOTYPE_GROUPS` - define to disable compound prototypes
- `QUANTUM_DISABLE_PROTOTYPE_GROUPS` - define to disable groups completely
- `FrameBase.GetEntityGroupIterator`
- `QuantumEntityViewNestedSelfViews` - not meant to be added directly, a component that tracks all the self-views in a group prototype
- More Quantum Unity 6 toolbar options

**Improvements**

- Improved the accuracy of many `FPVector2`, `FPVector3` and `FPQuaternion` methods by reducing rounding errors
- Significantly improved the accuracy of `FPVector2.` and `FPVector3.Angle` methods in some cases. Also added `FPVector2.` and `FPVector3.AngleSkipNormalize`

**Changes**

- `MapEntityId.SceneIndexPlusOne` is now obsolete, use `MapEntityId.RawValue` instead
- `QuantumStateInspector` logs a warning if a debug command is failed to be sent

**Bug Fixes**

- Fixed: Invalid `EntityPrototypeRef` resolution

### Build 2005 (Mar 12, 2026)

**Improvements**

- Less overhead when iterating component filters

### Build 2003 (Mar 07, 2026)

**Bug Fixes**

- Fixed: An issue in 2D and 3D Physics that caused pointer corruption when physics table components were moved in memory

### Build 2002 (Mar 06, 2026)

**What's New**

- Make public a method to move entity in advance of adding multiple table components

### Build 1997 (Feb 25, 2026)

**Bug Fixes**

- Fixed: An issue where `frame.DestroyPending(entity)` was giving incorrect results (again). When `frame.Destroy(entity)` is called, `frame.DestroyPending(entity)` is expected return `true` during `Destroy` (remove callbacks) and afterward until the destroy is committed. Before preview build 1946, `DestroyPending` did return `true` during `Destroy` but `false` afterward. After build 1946, it returned `true` after `Destroy` but `false` during. With this fix, `DestroyPending` returns the correct result in both scenarios
- Fixed: Possible `NullReferenceException` in `AssetRefDrawer` when used with Odin drawers
- Fixed: An issue in 2D and 3D Physics Callbacks that caused OnEnter callbacks to not be invoked when a collider got disabled and re-enabled after a few frames

### Build 1994 (Feb 20, 2026)

**Bug Fixes**

- Fixed: Recurrent allocations in Debug asserts when acquiring names of component sets

### Build 1990 (Feb 17, 2026)

**What's New**

- An experimental feature to use a customized `SessionRunner.Arguments.SnapshotProvider` for buddy snapshots that can run on a background thread, only advised to used without changes to `FrameContext`

**Bug Fixes**

- Fixed: An issue exporting Quantum DotNet project with dotnet version `10.0.2`

### Build 1986 (Feb 13, 2026)

**Changes**

- Adding table components to an entity (or committing their removal) while a filter is iterating that entity's current table now throws an exception. To work around this, use the new `frame.DeferredAdd(entity, component)` and `frame.DeferredSet(entity, component)` methods to enqueue changes, then optionally call `frame.Unsafe.CommitDeferred()` outside the filter loop to commit them. If not committed manually, they'll be committed at the usual point together with entity destroys and component removes

**Bug Fixes**

- Fixed: An editor peformance issue with `UnityNavMeshArea` drawer attribute

### Build 1981 (Feb 11, 2026)

**Bug Fixes**

- Fixed: An issue in `DelaunayTriangulation` that could lead to less flipped edges

### Build 1978 (Feb 10, 2026)

**Bug Fixes**

- Fixed: An issue that could cause commands to be missing on verified frames using the Quantum debug dlls

### Build 1974 (Feb 04, 2026)

**What's New**

- `InvokeSpeculativeCallbacks` setting in the Simulation Config (enabled by default) that allows CCD callbacks to be called even after the first non-trigger collision. `IsSpeculativeCcdCollision` property in callback info structs can be used to check this condition if the setting is enabled

### Build 1972 (Feb 03, 2026)

**Bug Fixes**

- Fixed: An issue that caused commands to be mispredicted way too often because sending them was delayed frequently

### Build 1971 (Jan 31, 2026)

**Removed**

- `DeterministicFrameSerializeMode` is obsolete now, affecting `Frame.Serialize()` methods, remove the mode parameter to migrate to

### Build 1968 (Jan 29, 2026)

**What's New**

- `QueryOption.SleepingOnly` is a new physics query option that, when used in combination with `HitDynamics` flag, causes awaken bodies to be ignored by the query

**Improvements**

- When awakened by dynamic collisions, physics bodies will now wake up other sleeping bodies that are in the same collision island in that same frame. Prevents issues with wake-ups not propagating over multiple frames in case the collision is not persistent

**Changes**

- 2D and 3D Physics Queries API that receive an external hit collection now have an optional parameter to reset those collections before resolving the query (used to be mandatory)

**Removed**

- Legacy `SessionContainer` class

**Bug Fixes**

- Fixed: An issue in `DelaunayTriangulation` that could lead to less flipped edges

### Build 1966 (Jan 27, 2026)

**What's New**

- Quantum graph profiler code can now be disabled to reduce the final build size by using the `QUANTUM_DISABLE_GRAPHPROFILER` scripting define

**Changes**

- Moved all GraphProfiler files to the `GraphProfiler` subfolder, this is not strictly required to migrate to

**Bug Fixes**

- Fixed: Issue in 2D and 3D PhysicsBody causing GravityScale to not be applied correctly

### Build 1962 (Jan 20, 2026)

**Bug Fixes**

- Fixed: The 2D Linecast query was detecting hits at the cast origin even when 'detectOverlapsAtCastOrigin' was disabled

### Build 1960 (Jan 13, 2026)

**Changes**

- Updated `FSharp.Core` assembly used in Quantum CodeGen

**Removed**

- Removed the `QuantumEditorConfig.editorconfig` file from the package, please manually delete the file in migrating projects

**Bug Fixes**

- Fixed: The 2D Linecast query was detecting hits at the cast origin even when 'detectOverlapsAtCastOrigin' was disabled

### Build 1957 (Jan 10, 2026)

**Changes**

- `DynamicAssetDB.AddAsset` - both overloads allow the asset to already have a guid assigned, as long as it is of `DynamicExplicit` type

### Build 1956 (Jan 09, 2026)

**What's New**

- `AddViewContext()` and `RemoveViewContext()` to the `QuantumEntityViewUpdater` to support alternatives way to register view contexts
- Adding a toolbar button to quickly open the Photon server settings

**Changes**

- Removed passing `QuantumEntityViewUpdater` into `QuantumEntityView.Initialize()`, instead set the `SnapshotInterpolationTimer`, which is actually required, later as a property

### Build 1950 (Jan 07, 2026)

**What's New**

- Added the attribute `[UnityNavMeshArea]` to assign a Unity navmesh area drawer

### Build 1949 (Jan 06, 2026)

**Bug Fixes**

- Fixed: An issue in 3D broad-phase queries against meshes that would cause an `AssertException` in Debug due to null triangle pointers

### Build 1946 (Jan 05, 2026)

**Improvements**

- Improved the accuracy of 2D and 3D ShapeCasts in some scenarios

**Bug Fixes**

- Fixed: Physics config layer matrix not being drawn correctly
- Fixed: `PropertyAttributes` not being applied on collections
- Fixed: `frame.DestroyPending(entity)` giving incorrect results

### Build 1942 (Dec 18, 2025)

**Bug Fixes**

- Fixed: `PropertyAttributes` not being applied on collections
- Fixed: Physics config layer matrix not being drawn correctly

### Build 1940 (Dec 16, 2025)

**What's New**

- Bringing back the Quantum open scene toolbar for Unity 6.3 embedded into Unity official toolbar API

### Build 1939 (Dec 15, 2025)

**What's New**

- `QUANTUM_DISABLE_ASSET_BUNDLE_ASSET_SOURCE` - adding this define will disable `QuantumAssetObjectSourceAssetBundle` 

**Bug Fixes**

- Fixed: `frame.DestroyPending(entity)` giving incorrect results
- Fixed: An issue where the frame (de)serializer skipped the table location info of entities pending destruction.  
This is really a `NullReferenceException` in disguise, but `Ptr.Null` is resolving to an actual pointer (to memory that has been filled with zeros) instead of to `null`. (That will be addressed in a separate fix.)

### Build 1935 (Dec 11, 2025)

**Bug Fixes**

- Fixed: An issue that disabled the Quantum toolbar for non-Unity 6.3 versions in the previous build
- Fixed: An issue that caused to import a Unity navmesh although toggled off in the map build chain

### Build 1929 (Dec 09, 2025)

**What's New**

- Added an `OnResync()` method to all Quantum systems, which is called when the simulation is about to begin after starting from a snapshots
- Asteroid and demo input scripts fully support Unity Input System

**Changes**

- 3D Physics `CollisionSolver.Solve` methods now perform multiple solver iterations by default, unless specified
- The Quantum open scene toolbar is disabled for Unity 6.3

**Bug Fixes**

- Fixed: Obsolete warnings in `QuantumUnityDBScopeImporter.cs` in Unity 6000.3

### Build 1925 (Dec 08, 2025)

**Bug Fixes**

- Fixed: Unity 6000.3 support

### Build 1922 (Dec 06, 2025)

**Bug Fixes**

- Fixed: An issue when using `AllocateOnComponentAdded` on collections inside Globals, which would make the Heap Tracker thrown an exception if the collection was expanded
- Fixed: The collision between polygons and polygons was not being detected when the edges of one of the polygons were perfectly aligned
- Fixed: The collision between circles and polygons was not being detected when the edges of the polygon were perfectly aligned, i.e., when the angle difference between two edges was 180 degrees
- Fixed: Invalid order of guids in `QuantumUnityDB.GetAssetInternal` assertion message
- Fixed: The intersection between 2D boxes was not generating a valid contact point, and the de-penetration direction was inverted

### Build 1919 (Dec 03, 2025)

**Breaking Changes**

- The API for sending and processing commands has changed so that multiple commands per frame can be send without requiring extra management using a `CompoundCommand`. Sending commands changes to `QuantumGame.AddCommand(DeterministicCommand)` and processing commands uses an iterator `foreach (var command in frame.GetPlayerCommands<MyCommand>(player)) { }`

**What's New**

- `QuantumQtnAssetImporter.UseCustomSettings`: enables per-file custom code generation. Can be used to isolate parts of Qtn into libraries. Refer to the class documentation for details and limitations

**Changes**

- [CodeGen] `GeneratorOptions.NewLine` is now an enum (was: string)
- [CodeGen] Imported components do not need their size specified as long as they are not used as fields in other components/structs. In other words, syntax `import component Foo;` is now valid
- [CodeGen] `EnsureNotStripped` generated method renamed to `EnsureNotStrippedGen`. Still not to be called directly
- [FrameContextUser] Update constructor signature to `FrameContextUser(Args args, IRuntimeConfig runtimeConfig)` and pass the runtime config from QuantumGame

**Bug Fixes**

- Fixed: Issues in `CollisionSolver.Solve` overloads that received a `CollisionResultInfo3D` and used an inverted normal, not solving the collision appropriately

### Build 1918 (Dec 02, 2025)

**What's New**

- `PhysicsSceneSettings` now has a `DefaultPhysicsMaterialData` initialized from the Physics Material asset defined in the Simulation Config

**Changes**

- `ISignal` interfaces and Quantum system classes now use the `frame` parameter name instead of abbreviating it with `f`

**Bug Fixes**

- Fixed: Issue in `CollisionSolver.Solve` overloads that did not receive a `Frame` parameter throwing `NullReferenceException` when at least one of the colliders did not have a valid Physics Material

### Build 1914 (Dec 01, 2025)

**What's New**

- Added more pre-defined `ColorRGBA` color variations that can be used in the debug `Draw()` from simulation utility

### Build 1913 (Nov 28, 2025)

**What's New**

- `FPMathUtils.TryLoadLookupTables`
- `QuantumGlobalScriptableObjectUtils.TryImportGlobal`
- Asset Bundle support - Quantum Unity DB recognizes assets that can be loaded by Asset Bundles. Use `QuantumAssetSourceAssetBundle` static delegates to change the way bundles themselves are loaded and unloaded

**Changes**

- `QuantumUnityDB` does not throw exceptions in `TryGet*` methods if the DB was failed to be loaded
- `QuantumUnityDB.OnEnable` no longer loads math lookup tables

**Bug Fixes**

- Fixed: An issue in the navmesh auto baking tools that tried importing a Unity navmesh when `ImportUnityNavMesh` is off and `BakeNavMesh` is on

### Build 1911 (Nov 27, 2025)

**Bug Fixes**

- Fixed: An exception thrown when a component is removed, re-added, and removed again before the first remove commits
- Fixed: An issue in 3.1.0 Preview 1908 that broke filters on sparse components

### Build 1909 (Nov 25, 2025)

**Bug Fixes**

- Fixed: Issues with Dynamic Maps when resetting the physics scene after adding static colliders
- Fixed: DB Scopes not importing the root asset when going through specified `AssetBundles`

### Build 1908 (Nov 24, 2025)

**Changes**

- `BitStreamReplayInputProvider.Stream` and `MaxFrame` are now public to make the class be re-usable to run a replay with chunked input history

**Bug Fixes**

- Fixed: A (3.1) issue where an entity being destroyed when it had pending removes resulted in those removes not being committed
- Fixed: A (3.1) issue where re-adding a sparse component pending removal would add another copy instead of overwriting the original

### Build 1907 (Nov 21, 2025)

**What's New**

- 3D Physics standalone solver API, available through the `Quantum.Physics3D.CollisionSolver` static class
- `FPVector2` and `FPVector3.Average` methods for computing the average of a span of vectors
- `PhysicsBody3D.ComputeWorldInertiaTensorInverse` to compute the world-space version of a physics body inverse inertia tensor

**Bug Fixes**

- Fixed: The intersection between 2D boxes was not generating a valid contact point, and the de-penetration direction was inverted

### Build 1904 (Nov 20, 2025)

**Changes**

- `QuantumCallbackHandler_UnityCallbacks.LoadAddressableScenePathsAsync` is now static

**Bug Fixes**

- Fixed: Addressable scenes not being unloaded correctly after loading same scene multiple times

### Build 1899 (Nov 18, 2025)

**What's New**

- 2D and 3D PhysicsCollider `Create` overload without Frame parameter for shapes other than Compounds
- Static methods `PhysicsMaterialData.GetCombinedRestitution` and `GetCombinedFriction` to retrieved the resultant settings from the interaction of two physics materials
- Static methods to get rows or columns from `FPMatrix2x2` or `FPMatrix3x3`

**Changes**

- Replaced the `ShowAfter` flags on the `QuantumDotnetBuildSettings` inspector with buttons that open the folder and solutions directly

**Bug Fixes**

- Fixed: An issue that caused the dotnet simulation to complain about old Quantum.Log dependencies in debug after migrating and not explicitly exporting the release configuration
- Fixed: A (3.1) issue where filters with a single component did not test an entity's `ComponentSet` correctly

### Build 1897 (Nov 18, 2025)

**Bug Fixes**

- Fixed: An issue in `TaskHandle.AddDependency` that would cause it to not raise an exception in Release and corrupt memory when going beyond the max number of parents or children

### Build 1896 (Nov 14, 2025)

**Improvements**

- Improved performance and stack memory usage when sorting 2D and 3D Hit collections

### Build 1894 (Nov 13, 2025)

**Changes**

- Removing a few internal Linq usages to reduce garbage allocations
- Improved the initialization of signal arrays to remove garbage allocations
- Renamed/shortened Quantum profiler marker names

### Build 1892 (Nov 11, 2025)

**What's New**

- `IntVector2` and `IntVector3` now have an index operator to access xy and z components
- `FPVector2` and `FPVector3` now have an index operator to access xy and z components
- Upgraded the graph profilers to combine multiple metrics in one graph to better analyse general simulation performance and online play
- Added new stats that measure simulation time spend on calculating predicted and verified frames explicitly (`DeterministicStats.PredictionTime` and `VerificationTime`)

**Changes**

- Updated Photon Realtime to a finalized version of `5.1.9 (10. November 2025)`

### Build 1889 (Nov 08, 2025)

**Bug Fixes**

- Fixed: ECS allocating too much memory upfront when creating and expanding tables

### Build 1888 (Nov 07, 2025)

**Bug Fixes**

- Fixed: An issue in `EditMeshScope.ReserveTriangleCapacity` when used in a `DynamicMap` that could cause an internal mismatch in the static collider index

### Build 1885 (Nov 05, 2025)

**What's New**

- `QuantumUnityDBScope`: an edit time collection of `AssetObjects`, fully decoupled from the global `QuantumUnityDB`. Once the scope is loaded at runtime, to apply it use `QuantumUnityDB.AddScope`
- `QuantumUnityDBScopeImporter.IncludeSubfolders` - if enabled, all assets in the current folder and all the subfolders will be included in the scope (true by default)
- `QuantumUnityDBScopeImporter.AssetBundles` - a list of asset bundles to be included in the scope
- `QuantumUnityDBScopeImporter.ExplicitAssets` - a list of assets to be included in the scope
- `QuantumUnityDB.TryAddScope`
- `QuantumUnityDB.IsScopeLoaded`
- `QuantumUnityDB.RemoveAllScopes`
- `QuantumUnityDBScopeImporter.IsUnique` - if an asset is part of multiple scopes and none of them are unique, the importer won't raise a warning (true by default)

**Changes**

- Adding a scope to `QuantumUnityDB` will print a warning if an asset can't be added due to GUID/path conflict. Previously: an exception would be thrown
- `QuantumUnityDB` reimport is speed up for cases where there aren't any package paths in `QuantumEditorSettings.AssetSearchPaths`

**Bug Fixes**

- Fixed: `QuantumUnityDB.RemoveScope` not removing entries correctly
- Fixed: Scoped assets displaying `<no provider>` under `Quantum Unity DB` section when inspected

### Build 1880 (Oct 31, 2025)

**Bug Fixes**

- Fixed: Addressable scenes not being released properly

### Build 1879 (Oct 30, 2025)

**Bug Fixes**

- Fixed: An issue where new `PageBasedHeap` segments would mistakenly think their blocks start from offset 0

### Build 1878 (Oct 29, 2025)

**Bug Fixes**

- Fixed: An issue where clients with certain heap configurations would desync upon late-joining

### Build 1873 (Oct 23, 2025)

**What's New**

- Support to scheduling multiple Physics Updates in the same frame simulation

### Build 1872 (Oct 21, 2025)

**Changes**

- The default heap management mode for migrating projects is now the new `PageBased` mode instead of

**Bug Fixes**

- Fixed: An issue where `FrameBase.ComponentCount` throws an `AssertException` for table components that have never been added to an entity

### Build 1871 (Oct 18, 2025)

**Bug Fixes**

- Fixed: A regression where ECS internals would GC allocate when iterating elements of `ComponentSet`
- Fixed: An issue that caused pause mode stepping in Unity Editor to not simulate one tick at a time

### Build 1870 (Oct 17, 2025)

**What's New**

- Added a NavMesh import option to load auto-generated navmesh links directly from the Unity navmesh, only works in Unity Editor
- Added `QuantumRunnerExtensions` methods to simplify starting Quantum in different modes by providing specific Init()-methods to create `SessionRunner.Arguments`, see the usage inside the `QuantumRunnerLocalDebug.cs` script for example
- Quantum instant replays now also work when being activated during a replay

**Changes**

- Renamed `QuantumGame.CreateSavegame()` to `GetSnapshotFile()`, retired the `QuantumRunnerLocalSavegame.cs` script and merged its functionality with `QuantumRunnerLocalDebug.cs`
- Corrected a typo in `QuantumRunnerUnityFactory.CreatePlatformInfo` and changed the static method to a property
- Removed the `StartWithFrame()` method from the `QuantumRunnerLocalDebug` class

**Bug Fixes**

- Fixed: A bug where multiple tasks dispatched from the same `SystemThreadedFilter` can visit the same entities. (The slice length was not being respected.)
- Fixed: An issue that caused the navmesh agent to chose any navmesh link instead of the closest one when having multiple links available that connects two triangles
- Fixed: An issue that caused the `QuantumRunnerLocalDebug` script to not apply the `SimulationSpeedMultiplier` when using `EngineDeltaTime`

### Build 1869 (Oct 16, 2025)

**What's New**

- `Quantum.Compression` - an abstract base class for compression algorithms. Comes with two implementations: `CompressionDotNet` (default) and `CompressionSharpLibZib` (enabled when `com.unity.sharp-zip-lib` package is present). Using the latter might help with "Runtime Speed with LTO" Web builds issues. Both implementations produce the same results and rely on `GZip` format

**Changes**

- `ByteUtils` compression methods made obsolete, use `Quantum.Compression` instead
- `CollisionChecks` in Physics2D and Physics3D namespaces are now static classes

**Bug Fixes**

- Fixed: System task profiler entries not being recorded in non-development builds

### Build 1865 (Oct 15, 2025)

**Bug Fixes**

- Fixed: A regression where filters didn't skip entities pending destruction
- Fixed: An issue that caused the heap settings in SimulationConfig of existing projects to not be migrated correctly

### Build 1863 (Oct 14, 2025)

**Breaking Changes**

- The `DeterministicSessionConfig` inspector now computes the `Hard Tolerance` based on the `Simulation Rate`, input `Offset Ping Start` and input `Offset Min`, to override this behaviour toggle `Override Hard Tolerance`

**What's New**

- Added `Prediction` statistic to the GraphProfilers and QuantumStats window that shows how many ticks the simulation goes into prediction

**Changes**

- Some stats on the QuantumStats window are now smoothed and show an average (1 second) to make them more readable

### Build 1862 (Oct 11, 2025)

**Bug Fixes**

- Fixed: An issue that could cause an ArgumentException similar to `X cannot be greater than Y` after late-joining

### Build 1861 (Oct 10, 2025)

**Bug Fixes**

- Fixed: An issue in the component block iterator that could cause the exception `_blockCount > 0`

### Build 1859 (Oct 09, 2025)

**Bug Fixes**

- Fixed: An issue in `QuantumStartUI` that caused multiple builds on the same machine that all used the same user name to not join the same room
- Fixed: An issue that caused the `QuantumStartUI` to show the popup window when stopping the Editor during connecting

