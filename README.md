# Photon to NGO migration reproduction project

This is a new, self-contained Unity demonstration of selected migration mechanisms
discussed in *Migrating a Multiplayer Educational VR Application from Photon PUN
to Unity Netcode for GameObjects*. It contains full source for the demonstration,
including scene and prefab generation, host/client setup, state replication,
tracking-target visualization, and CSV recording.

**Provenance:** reconstructed for the manuscript revision. This is not recovered
source for the original biology or ride application. It does not recreate the
original Photon implementation, historical incidents, educational assets, or
original IK solver. Its results must be reported separately as new tests.

**Execution status:** Unity is not installed in the authoring environment, so the
project has not been imported, compiled by Unity, built, or tested in a live
host/client session here. Static C# syntax checks and log-analyzer tests are
documented in `Docs/VALIDATION.md`. They do not establish runtime success.

## Open and generate

1. Extract this folder and add it as a project in Unity Hub.
2. Open with **Unity 6000.4.0f1**. Package Manager will resolve NGO **2.10.0** and
   Unity Transport **2.6.0** from `Packages/manifest.json`. Allow import to finish.
3. The editor automatically generates the demonstration on first import. If it
   does not, choose **Migration Lab > Generate Demo Scenes and Prefabs**.
4. Open `Assets/MigrationLab/Generated/LabBootstrap.unity` and press Play.
5. Use the generated build scenes in the order Bootstrap, Scene A, Scene B. The
   generator configures this order automatically. Do not start in Scene A/B.

Generated `.unity`, `.prefab`, and `.mat` assets are produced by the included
editor code. No manual prefab wiring or proprietary assets are required. Unity
creates other default settings and `.meta` files on import. Once validated,
retain those files and `Packages/packages-lock.json` in your published release.
Regeneration overwrites only the `Assets/MigrationLab/Generated` demonstration
assets and updates the project build-scene list; use this as a separate project.

## Run two processes

Use an Editor host and a standalone client, or two standalone processes. Two
instances of the Unity Editor cannot open the same project directory.

1. Build for your installed desktop target using **File > Build Profiles**, or
   the batch build method below. Include the three generated scenes.
2. In the Editor, click **Start Host**, then wait until Scene A is ready.
3. Run the desktop build. Leave address `127.0.0.1` and port `7777`; click
   **Start Client**. For another computer on a private LAN, use the host's LAN IP.
4. Give both processes the same run label and scenario label before recording.
5. Move the three desktop tracking sliders independently in each process.
   Observe that remote targets follow their participant; exchange roles.

Local/LAN transport requires no Unity Services account, Relay allocation, API key,
or paid asset. This demonstration does not implement Relay or internet discovery.
Connection failures may require checking the OS firewall and selected address.

Optional command-line launch for desktop builds:

```sh
./MigrationLab.x86_64 --lab-role host --lab-run scene-001 --lab-scenario scene_transition
./MigrationLab.x86_64 --lab-role client --lab-address 127.0.0.1 --lab-run scene-001 --lab-scenario scene_transition
```

Use the corresponding `.exe` on Windows. On macOS, launch the executable inside
`MigrationLab.app/Contents/MacOS/`. Add `--lab-omit-initial` **only on the client**
to select the intentionally faulty initial-state path before joining.

Batch build, with an installed Unity Editor and licensed build support:

```sh
Unity -batchmode -projectPath /absolute/path/PhotonNGO_Reproduction -buildTarget StandaloneLinux64 -executeMethod MigrationLab.Editor.LabProjectBuilder.Build -quit -logFile build.log
```

Use `StandaloneWindows64` or `StandaloneOSX` as appropriate. The executable and
supporting data are written under `Builds/`. Inspect the build log and exit status.

## What the controls demonstrate

| Mechanism | Implementation | Comparison or observation |
|---|---|---|
| Authority and roles | Server-owned session; client requests carry NGO sender metadata | Guide advances stops; guest navigation is rejected |
| Shared interactions | Server checks the student-permission flag before incrementing a shared counter | Toggle permission as host; observe accepted/rejected guest requests |
| Late joining | `OnNetworkSpawn` applies the current replicated stop | Pre-join fault switch omits initial application; future changes still work |
| Scene transitions | Server calls NGO scene loading; event status/completions/timeouts are logged | Transition A/B and compare each client's state and placement |
| Local lifecycle | One persistent local rig; placement deferred until destination and anchor exist | Observe no duplicate rig after transitions |
| Shared continuity | Persistent, server-written session variables | Late join after a transition and inspect the retained stop/counter |
| Host loss | No recovery is implemented | Disconnect the host and record client session termination |
| Participant association | Owner-written head/hand target poses; remote clients consume received targets | Local fault switch makes remote avatars incorrectly read the viewer's targets |
| Offline path | Explicit offline action with local scene loading | It is separate from connected-client network coordination |

The shared-state model is a newly implemented extension, not evidence that the
historical biology migration already provided that functionality. Host recovery
and a production command-adapter abstraction remain outside this demonstration.

## Desktop and XR boundaries

The delivered default is a **desktop networking demonstrator**. A persistent
observer camera looks at the participants; it is separate from their simulated
tracking targets. Grey primitive bodies and lines visualize received targets;
they are not a full-body IK solver, reconstructed avatar geometry, or a test of
animation accuracy. No NetworkTransform or per-bone transform replication is
needed for this selected target-association demonstration.

The optional `Read tracked XR devices` switch reads Unity XR head/hand device
poses if a valid XR loader is configured. Missing tracked devices fall back to
desktop values and produce `xr_input_missing` log events. **No OpenXR loader,
headset rendering, controller interaction system, room calibration, or Android
build configuration is supplied or validated.** Do not describe desktop tests as
Quest headset or immersive-VR validation. For those tests, first integrate and
verify the appropriate XR setup as a separately documented change.

All transmitted target positions and rotations use the demonstration's world
coordinate frame and an identity-scale persistent rig. A multi-room physical XR
deployment requires explicit spatial calibration beyond this example.

## Record and analyze

Follow `Docs/TEST_PROTOCOL.md`. Every process writes a CSV under
`Application.persistentDataPath/MigrationLabLogs`; the full path is shown in the
application and Unity/player console. Record run labels, roles, builds, network
conditions, and expected outcomes before the check.

The placement check requires scene agreement, matching applied/replicated stop,
an available anchor, and origin error of at most **0.01 m** after the explicit
per-participant lateral offset. This is a software-origin check in the synthetic
scene, not measured human tracking accuracy. The offset separates avatars and
is `client ID modulo 4 × 1.2 m`; use at most four connected participants.

Authorization checks compare the latest completed reply with the expected
acceptance toggle. Avatar buttons record an **observer's manual judgment**; they
are not automated motion-accuracy measurements. Session-ended checks test only
termination after host loss. Record failures and missing observations too.

```sh
python3 Tools/summarize_logs.py /path/to/collected/logs --output summary.json
python3 -m unittest discover -s Tools -p 'test_*.py' -v
```

The summarizer counts scored check records and warns about repeated checks with
the same run/scenario/role/event label. It does not infer independent trials,
confidence intervals, symptom improvement, or general reliability. An unscored
event is never counted as a pass. Synthetic fixtures exist only inside the
analyzer's unit tests; no experimental results are included in this release.

## Source files

- `LabProjectBuilder.cs`: creates all scene, prefab, material, and build references.
- `LabBootstrap.cs`: local rig, readiness, launch controls, input, and check recording.
- `LabSession.cs`: replicated lesson state, validated commands, and scene events.
- `LabAvatar.cs`: serialized target poses and participant-associated visualization.
- `LabAnchor.cs`: destination stop anchors.
- `LabLog.cs`: independent CSV file per process.
- `Tools/`: log summarizer and software unit tests.

## Publication use

Keep this project in a clearly named reconstructed-examples folder or a separate
release, preserve the existing historical excerpts, and archive the actual
validated release. Use `Docs/RELEASE_CHECKLIST.md` before changing manuscript
claims. Adding code alone does not establish that repeated tests have run.
