using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace MigrationLab
{
    public sealed class LabBootstrap : MonoBehaviour
    {
        public static LabBootstrap Instance { get; private set; }
        public NetworkManager Manager;
        public UnityTransport Transport;
        public GameObject SessionPrefab;
        public bool OmitInitialState;
        public bool WrongRemoteInputs;
        public bool ReadXRDevices;
        public bool? LastRequestAccepted;
        public bool ExpectedAcceptance = true;
        public string Notice = "Start a host, then a client in a second process.";
        public int AppliedStop { get; private set; } = -1;
        public float AnchorError { get; private set; } = -1;
        private Transform rig;
        private int desiredStop = -1;
        private bool placementPending;
        private bool offline;
        private string address = "127.0.0.1";
        private string port = "7777";
        private string run = "trial-001";
        private string scenario = "late_join_corrected";
        private float headYaw;
        private float leftHeight;
        private float rightHeight;
        private float lastInvalidXRLog = -10;
        private Vector2 scroll;
        private float lastRequestTime = -1;
        private bool hadConnectedSession;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameObject origin = new GameObject("PersistentLocalRig");
            origin.transform.SetParent(transform, false); rig = origin.transform;
            rig.position = new Vector3(-8, 0, -8);
            var cameraObject = new GameObject("LocalCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(rig, false);
            // Desktop observer view. The tracking origin remains independent of this camera.
            cameraObject.transform.localPosition = new Vector3(1.5f, 3, -7);
            cameraObject.transform.localRotation = Quaternion.Euler(12, 0, 0);
            var camera = cameraObject.AddComponent<Camera>(); camera.fieldOfView = 75;
            cameraObject.AddComponent<AudioListener>();
            SceneManager.sceneLoaded += SceneLoaded;
            Manager.OnServerStarted += ServerStarted;
            Manager.OnClientConnectedCallback += ClientConnected;
            Manager.OnClientDisconnectCallback += ClientDisconnected;
            Manager.OnClientStopped += ClientStopped;
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            string role = "";
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--lab-role") role = args[i + 1];
                if (args[i] == "--lab-address") address = args[i + 1];
                if (args[i] == "--lab-port") port = args[i + 1];
                if (args[i] == "--lab-run") run = args[i + 1];
                if (args[i] == "--lab-scenario") scenario = args[i + 1];
            }
            OmitInitialState = Array.IndexOf(args, "--lab-omit-initial") >= 0;
            LabLog.Run = run; LabLog.Scenario = scenario;
            LabLog.Write("process_start", "Unity=" + Application.unityVersion +
                "; platform=" + Application.platform + "; version=" + Application.version);
            yield return null;
            if (role == "host") StartNetwork(true);
            else if (role == "client") StartNetwork(false);
            else if (role == "offline") StartOffline();
        }

        private void StartNetwork(bool host)
        {
            if (Manager.IsListening || offline) return;
            if (!ushort.TryParse(port, out ushort number) || number == 0) { Notice = "Port must be 1–65535."; return; }
            OmitInitialState = !host && OmitInitialState;
            Transport.SetConnectionData(address, number, host ? "0.0.0.0" : null);
            bool ok = host ? Manager.StartHost() : Manager.StartClient();
            Notice = ok ? "Network start requested." : "Start failed. See Unity/player log.";
            LabLog.Write("network_start", "host=" + host + "; address=" + address + "; port=" + number + "; returned=" + ok);
        }

        private void ServerStarted()
        {
            var session = Instantiate(SessionPrefab);
            session.GetComponent<NetworkObject>().Spawn(false);
            StartCoroutine(InitialScene());
        }

        private IEnumerator InitialScene()
        {
            yield return null;
            if (LabSession.Instance != null) LabSession.Instance.LoadDestination("LabSceneA");
        }

        private void ClientConnected(ulong id)
        {
            if (id == Manager.LocalClientId) hadConnectedSession = true;
            LabLog.Write("client_connected", id.ToString());
        }
        private void ClientDisconnected(ulong id)
        {
            LabLog.Write("client_disconnected", id.ToString());
            if (!Manager.IsServer && id == Manager.LocalClientId) Notice = "Disconnected: session has no host recovery.";
        }
        private void ClientStopped(bool wasHost)
        {
            LabLog.Write("client_stopped", "was_host=" + wasHost);
            desiredStop = -1; AppliedStop = -1; AnchorError = -1; placementPending = false;
            Notice = "Session stopped. No host migration is implemented.";
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            placementPending = true; AppliedStop = -1; AnchorError = -1;
            LabLog.Write("local_scene_loaded", scene.name + "; mode=" + mode);
        }

        public void QueueStop(int value) { desiredStop = value; placementPending = true; }
        public void InvalidatePlacement() { placementPending = true; AppliedStop = -1; AnchorError = -1; }

        private Vector3 ParticipantOffset => new Vector3(offline ? 0 : (Manager.LocalClientId % 4) * 1.2f, 0, 0);

        private void Update()
        {
            if (!placementPending || desiredStop < 0) return;
            var session = LabSession.Instance;
            if (!offline && (session == null || !session.IsSpawned ||
                SceneManager.GetActiveScene().name != session.Destination.Value.ToString())) return;
            LabAnchor anchors = FindFirstObjectByType<LabAnchor>();
            Transform target = anchors == null ? null : anchors.GetStop(desiredStop);
            if (target == null) return; // Dependencies are not ready: leave request pending.
            rig.SetPositionAndRotation(target.position + ParticipantOffset, target.rotation);
            AppliedStop = desiredStop; placementPending = false;
            AnchorError = Vector3.Distance(rig.position, target.position + ParticipantOffset);
            LabLog.Write("rig_placed", "stop=" + AppliedStop + "; scene=" + SceneManager.GetActiveScene().name);
        }

        public void GetLocalTargets(out TargetPose head, out TargetPose left, out TargetPose right)
        {
            Vector3 hp = new Vector3(0, 1.65f, 0), lp = new Vector3(-0.45f, 1.1f + leftHeight, 0.6f),
                rp = new Vector3(0.45f, 1.1f + rightHeight, 0.6f);
            Quaternion hr = Quaternion.Euler(0, headYaw, 0), lr = Quaternion.identity, rr = Quaternion.identity;
            if (ReadXRDevices)
            {
                bool valid = ReadNode(XRNode.Head, ref hp, ref hr) & ReadNode(XRNode.LeftHand, ref lp, ref lr)
                    & ReadNode(XRNode.RightHand, ref rp, ref rr);
                if (!valid && Time.unscaledTime > lastInvalidXRLog + 5)
                { lastInvalidXRLog = Time.unscaledTime; LabLog.Write("xr_input_missing", "One or more targets use desktop fallback values."); }
            }
            head = new TargetPose(rig.TransformPoint(hp), rig.rotation * hr);
            left = new TargetPose(rig.TransformPoint(lp), rig.rotation * lr);
            right = new TargetPose(rig.TransformPoint(rp), rig.rotation * rr);
        }

        private static bool ReadNode(XRNode node, ref Vector3 position, ref Quaternion rotation)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked) return false;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q)) return false;
            position = p; rotation = q; return true;
        }

        private void StartOffline()
        {
            if (Manager.IsListening) return;
            offline = true; QueueStop(0); SceneManager.LoadScene("LabSceneA");
            Notice = "Local offline demonstration. No replication or authority comparison.";
            LabLog.Write("offline_started");
        }

        private void RecordPlacement()
        {
            var session = LabSession.Instance;
            int expected = offline ? desiredStop : session == null ? -1 : session.Stop.Value;
            LabAnchor anchors = FindFirstObjectByType<LabAnchor>();
            var target = anchors == null ? null : anchors.GetStop(expected);
            AnchorError = target == null ? -1 : Vector3.Distance(rig.position, target.position + ParticipantOffset);
            bool sceneOK = offline || (session != null && SceneManager.GetActiveScene().name == session.Destination.Value.ToString());
            bool pass = sceneOK && expected >= 0 && AppliedStop == expected && target != null && AnchorError <= 0.01f;
            LabLog.Write("placement_check", "tolerance_m=0.01; scene_ok=" + sceneOK +
                "; omit_initial=" + OmitInitialState + "; wrong_remote_inputs=" + WrongRemoteInputs, pass);
            Notice = pass ? "Placement check PASS (logged)." : "Placement check FAIL (logged).";
        }

        private void Command(int op, int argument = 0)
        {
            if (Time.unscaledTime - lastRequestTime < 0.15f) return;
            lastRequestTime = Time.unscaledTime;
            if (LabSession.Instance != null) LabSession.Instance.Request(op, argument);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 520, Mathf.Min(Screen.height - 24, 850)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("PHOTON → NGO MIGRATION LAB — RECONSTRUCTED EXAMPLE");
            GUILayout.Label("Run label (same in all processes):"); run = GUILayout.TextField(run);
            GUILayout.Label("Scenario label:"); scenario = GUILayout.TextField(scenario);
            LabLog.Run = run; LabLog.Scenario = scenario;
            GUILayout.Label("Address / UDP port:"); address = GUILayout.TextField(address); port = GUILayout.TextField(port);
            bool connected = Manager.IsListening;
            GUI.enabled = !connected && !offline;
            OmitInitialState = GUILayout.Toggle(OmitInitialState, "Fault: omit current state on client spawn (set before joining)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Host")) StartNetwork(true);
            if (GUILayout.Button("Start Client")) StartNetwork(false);
            if (GUILayout.Button("Offline")) StartOffline();
            GUILayout.EndHorizontal();
            GUI.enabled = connected;
            if (GUILayout.Button("Disconnect / end host")) { LabLog.Write("shutdown_requested"); Manager.Shutdown(); }
            GUI.enabled = connected && LabSession.Instance != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Request next stop")) Command(0);
            if (GUILayout.Button("Request shared action")) Command(1);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Request toggle student permission (guide only)")) Command(2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Request Scene A")) Command(3, 0);
            if (GUILayout.Button("Request Scene B")) Command(3, 1);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            var session = LabSession.Instance;
            GUILayout.Label("Network stop: " + (session == null ? "none" : session.Stop.Value.ToString()) +
                " | Applied stop: " + AppliedStop + " | Scene: " + SceneManager.GetActiveScene().name);
            GUILayout.Label("Shared actions: " + (session == null ? "none" : session.InteractionCount.Value.ToString()) +
                " | Student permission: " + (session == null ? "none" : session.StudentInteraction.Value.ToString()));
            WrongRemoteInputs = GUILayout.Toggle(WrongRemoteInputs, "Fault: render remote avatars from this device's inputs");
            ReadXRDevices = GUILayout.Toggle(ReadXRDevices, "Read tracked XR devices (requires a configured XR loader)");
            GUILayout.Label("Desktop tracking controls: head yaw / left hand height / right hand height");
            headYaw = GUILayout.HorizontalSlider(headYaw, -80, 80);
            leftHeight = GUILayout.HorizontalSlider(leftHeight, -0.4f, 0.8f);
            rightHeight = GUILayout.HorizontalSlider(rightHeight, -0.4f, 0.8f);
            if (GUILayout.Button("Record scene / stop / placement check")) RecordPlacement();
            ExpectedAcceptance = GUILayout.Toggle(ExpectedAcceptance, "Expected latest server reply: accepted");
            if (GUILayout.Button("Record latest authorization reply check"))
            {
                if (!LastRequestAccepted.HasValue) Notice = "No completed request to assess. Send one and wait for the reply.";
                else LabLog.Write("authorization_check", "expected=" + ExpectedAcceptance +
                    "; observed=" + LastRequestAccepted.Value, LastRequestAccepted.Value == ExpectedAcceptance);
            }
            if (GUILayout.Button("Record session-ended check after host loss"))
                LabLog.Write("host_loss_check", "Checks session termination only; not recovery.",
                    hadConnectedSession && !Manager.IsListening && !offline);
            if (GUILayout.Button("Record visual avatar PASS (observer judgment)")) LabLog.Write("avatar_manual", "observer marked correct participant association", true);
            if (GUILayout.Button("Record visual avatar FAIL (observer judgment)")) LabLog.Write("avatar_manual", "observer marked incorrect participant association", false);
            if (offline)
            {
                if (GUILayout.Button("Offline next stop")) QueueStop((desiredStop + 1) % 3);
                if (GUILayout.Button("Exit offline mode")) { offline = false; InvalidatePlacement(); desiredStop = -1; }
            }
            GUILayout.Label(Notice);
            GUILayout.Label("CSV: " + (LabLog.FilePath ?? "not opened"));
            GUILayout.Label("This is a desktop networking demonstrator, not the original biology application or a validated VR/IK system.");
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Manager != null)
            {
                Manager.OnServerStarted -= ServerStarted;
                Manager.OnClientConnectedCallback -= ClientConnected;
                Manager.OnClientDisconnectCallback -= ClientDisconnected;
                Manager.OnClientStopped -= ClientStopped;
            }
            Instance = null;
        }
        private void OnApplicationQuit() { LabLog.Write("process_exit"); LabLog.Close(); }
    }
}
