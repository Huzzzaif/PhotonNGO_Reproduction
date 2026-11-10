# Validation status

## Completed in the code-authoring environment

- All six C# files parsed without syntax errors using the tree-sitter C# grammar.
  This checks grammar only; it is not C# semantic compilation or Unity compilation.
- Four Python log-summary unit tests passed: failures remain counted, unscored
  events do not become passes, repeated check records produce a warning rather
  than independent-trial counts, invalid scores are rejected, and empty logs do
  not imply success (the failure/unscored assertions share one test).
- The package manifest was parsed and version pins checked against the official
  NGO 2.10.0 package metadata. NGO 2.10.0 declares Unity Transport 2.6.0.
- Used public NGO/UTP members were inspected in the official downloaded package
  source: NetworkConfig/NetworkPrefabs, NetworkObject.Spawn, NetworkBehaviour
  lifecycle, RpcAttribute/RpcInvokePermission, RpcParams, SceneEvent,
  NetworkSceneManager.LoadScene, and UnityTransport.SetConnectionData.
- ZIP content and SHA-256 file inventory are supplied for artifact inspection.

Reference package endpoint:
https://packages.unity.com/com.unity.netcode.gameobjects/-/com.unity.netcode.gameobjects-2.10.0.tgz

Reference API documentation:
https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.10/manual/index.html

## Not completed

Unity Editor is not installed in the code-authoring environment. Therefore Unity
package resolution/import, C# semantic compilation and NGO code generation,
scene/prefab generation, desktop build, multi-process execution, headset use,
fault comparisons, and repeated experimental trials have not been performed.

No experimental CSV data or measured success rates are supplied. Python unit
fixtures are synthetic software tests and must never be presented as Unity data.

## Required next validation

Follow the README, TEST_PROTOCOL.md, and RELEASE_CHECKLIST.md on a machine with
the specified Unity Editor. Retain compiler/build logs, generated project files,
the actual package lock, independent trial records, and raw process CSV files.
If fixes are needed, record the changed source revision and rerun affected checks.
