using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MigrationLab.Editor
{
    [InitializeOnLoad]
    public static class LabProjectBuilder
    {
        private const string Root = "Assets/MigrationLab/Generated";
        static LabProjectBuilder() { EditorApplication.delayCall += GenerateIfMissing; }

        private static void GenerateIfMissing()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(Root + "/LabBootstrap.unity")) Generate();
        }

        [MenuItem("Migration Lab/Generate Demo Scenes and Prefabs")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before generating.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root);
            PlayerSettings.companyName = "MigrationResearch";
            PlayerSettings.productName = "PhotonNGOReproduction";
            PlayerSettings.bundleVersion = "0.1.0-reconstructed";
            PlayerSettings.runInBackground = true;
            var sessionObject = new GameObject("PersistentSession");
            sessionObject.AddComponent<NetworkObject>(); sessionObject.AddComponent<LabSession>();
            GameObject session = PrefabUtility.SaveAsPrefabAsset(sessionObject, Root + "/Session.prefab");
            Object.DestroyImmediate(sessionObject);

            var avatarObject = new GameObject("ParticipantAvatar");
            avatarObject.AddComponent<NetworkObject>();
            var avatar = avatarObject.AddComponent<LabAvatar>();
            avatar.Head = Shape(avatarObject.transform, "Head", PrimitiveType.Sphere, new Vector3(.25f, .25f, .25f));
            avatar.LeftHand = Shape(avatarObject.transform, "LeftHand", PrimitiveType.Cube, Vector3.one * .14f);
            avatar.RightHand = Shape(avatarObject.transform, "RightHand", PrimitiveType.Cube, Vector3.one * .14f);
            avatar.Body = Shape(avatarObject.transform, "Body", PrimitiveType.Cube, new Vector3(.4f, .6f, .2f));
            avatar.LeftArm = Arm(avatarObject.transform, "LeftArm");
            avatar.RightArm = Arm(avatarObject.transform, "RightArm");
            GameObject player = PrefabUtility.SaveAsPrefabAsset(avatarObject, Root + "/Avatar.prefab");
            Object.DestroyImmediate(avatarObject);

            Scene boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var managerObject = new GameObject("NetworkAndLocalRig");
            var transport = managerObject.AddComponent<UnityTransport>();
            var manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = player,
                EnableSceneManagement = true, TickRate = 30 };
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = session });
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = player });
            var bootstrap = managerObject.AddComponent<LabBootstrap>();
            bootstrap.Manager = manager; bootstrap.Transport = transport; bootstrap.SessionPrefab = session;
            EditorSceneManager.SaveScene(boot, Root + "/LabBootstrap.unity");
            MakeWorld("LabSceneA", new Color(.25f, .5f, .3f));
            MakeWorld("LabSceneB", new Color(.25f, .35f, .65f));
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(Root + "/LabBootstrap.unity", true),
                new EditorBuildSettingsScene(Root + "/LabSceneA.unity", true),
                new EditorBuildSettingsScene(Root + "/LabSceneB.unity", true) };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Root + "/LabBootstrap.unity");
            Debug.Log("Migration Lab generated. Press Play, start Host, then run a standalone Client.");
        }

        private static Transform Shape(Transform parent, string name, PrimitiveType kind, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(kind); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
        }

        private static LineRenderer Arm(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2; line.useWorldSpace = true; line.startWidth = .05f; line.endWidth = .05f;
            line.sharedMaterial = Material(Color.white, name); return line;
        }

        private static Material Material(Color color, string name)
        {
            string path = Root + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static void MakeWorld(string name, Color color)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = name + "Floor"; floor.transform.localScale = new Vector3(4, 1, 4);
            floor.GetComponent<Renderer>().sharedMaterial = Material(color, name + "Floor");
            var anchors = new GameObject("DestinationAnchors").AddComponent<LabAnchor>();
            anchors.Stops = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var stop = new GameObject("Stop" + i); stop.transform.SetParent(anchors.transform);
                stop.transform.position = new Vector3(i * 4 - 4, 0, -3);
                anchors.Stops[i] = stop.transform;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "StopMarker" + i; marker.transform.position = new Vector3(i * 4 - 4, .2f, 3);
                marker.transform.localScale = new Vector3(1, .4f, 1);
                marker.GetComponent<Renderer>().sharedMaterial = Material(Color.Lerp(Color.red, Color.yellow, i / 2f), "Marker" + i);
                var label = new GameObject("StopLabel").AddComponent<TextMesh>();
                label.text = name + " / stop " + i; label.characterSize = .15f; label.fontSize = 48;
                label.transform.position = marker.transform.position + Vector3.up;
                label.transform.rotation = Quaternion.Euler(0, 180, 0);
                label.anchor = TextAnchor.MiddleCenter;
            }
            EditorSceneManager.SaveScene(scene, Root + "/" + name + ".unity");
        }

        // Example: Unity -batchmode -projectPath PATH -executeMethod MigrationLab.Editor.LabProjectBuilder.Build -quit -logFile build.log
        public static void Build()
        {
            Generate();
            string filename = EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 ? "MigrationLab.exe" :
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX ? "MigrationLab.app" : "MigrationLab.x86_64";
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/" + filename,
                EditorUserBuildSettings.activeBuildTarget, BuildOptions.Development);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Build failed: " + report.summary.result);
        }
    }
}
