using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MigrationLab
{
    // All instructional state is server-written. Client identity comes from RPC metadata.
    public sealed class LabSession : NetworkBehaviour
    {
        public static LabSession Instance { get; private set; }
        public readonly NetworkVariable<int> Stop = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> InteractionCount = new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> StudentInteraction = new NetworkVariable<bool>(true);
        public readonly NetworkVariable<FixedString64Bytes> Destination =
            new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes("LabSceneA"));
        private bool subscribed;
        private bool loading;
        private int requestSerial;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Stop.OnValueChanged += StopChanged;
            Destination.OnValueChanged += DestinationChanged;
            NetworkManager.SceneManager.OnSceneEvent += SceneEvent;
            subscribed = true;
            bool omit = !IsServer && LabBootstrap.Instance.OmitInitialState;
            if (!omit) LabBootstrap.Instance.QueueStop(Stop.Value);
            LabLog.Write("session_spawn", "initial state " + (omit ? "deliberately omitted" : "applied") +
                "; mode=" + (omit ? "faulty" : "corrected"));
        }

        public override void OnNetworkDespawn()
        {
            if (subscribed)
            {
                Stop.OnValueChanged -= StopChanged;
                Destination.OnValueChanged -= DestinationChanged;
                if (NetworkManager != null && NetworkManager.SceneManager != null)
                    NetworkManager.SceneManager.OnSceneEvent -= SceneEvent;
                subscribed = false;
            }
            if (Instance == this) Instance = null;
        }

        private void StopChanged(int previous, int current)
        {
            LabBootstrap.Instance.QueueStop(current);
            LabLog.Write("stop_received", previous + " -> " + current);
        }

        private void DestinationChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            LabBootstrap.Instance.InvalidatePlacement();
            LabLog.Write("destination_received", current.ToString());
        }

        private void SceneEvent(SceneEvent e)
        {
            int complete = e.ClientsThatCompleted == null ? 0 : e.ClientsThatCompleted.Count;
            int timeout = e.ClientsThatTimedOut == null ? 0 : e.ClientsThatTimedOut.Count;
            LabLog.Write("scene_event", e.SceneEventType + "; scene=" + e.SceneName +
                "; event_client=" + e.ClientId + "; completed=" + complete + "; timed_out=" + timeout);
            if (IsServer && e.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                loading = false;
                LabLog.Write("scene_group_complete", "scene=" + e.SceneName + "; timed_out=" + timeout, timeout == 0);
            }
        }

        public void Request(int operation, int argument = 0)
        {
            if (!IsSpawned) return;
            requestSerial++;
            LabBootstrap.Instance.LastRequestAccepted = null;
            LabLog.Write("request_sent", "request=" + requestSerial + "; operation=" + operation);
            CommandRpc(operation, argument, requestSerial);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void CommandRpc(int operation, int argument, int serial, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            bool guide = sender == Unity.Netcode.NetworkManager.ServerClientId;
            bool allowed = NetworkManager.ConnectedClients.ContainsKey(sender) &&
                (guide || (operation == 1 && StudentInteraction.Value));
            if (loading) allowed = false;
            if (operation < 0 || operation > 3) allowed = false;
            if (!allowed)
            {
                LabLog.Write("request_rejected", "sender=" + sender + "; request=" + serial + "; operation=" + operation);
                ReplyRpc(sender, serial, false, "Rejected by application policy or scene not ready.");
                return;
            }
            switch (operation)
            {
                case 0: Stop.Value = (Stop.Value + 1) % 3; break;
                case 1: InteractionCount.Value++; break;
                case 2: StudentInteraction.Value = !StudentInteraction.Value; break;
                case 3:
                    string scene = argument == 0 ? "LabSceneA" : "LabSceneB";
                    if (!LoadDestination(scene))
                    {
                        ReplyRpc(sender, serial, false, "Scene request did not start."); return;
                    }
                    break;
            }
            LabLog.Write("request_accepted", "sender=" + sender + "; request=" + serial + "; operation=" + operation);
            ReplyRpc(sender, serial, true, "Accepted by server.");
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ReplyRpc(ulong recipient, int serial, bool accepted, string message)
        {
            if (NetworkManager.LocalClientId != recipient) return;
            LabBootstrap.Instance.LastRequestAccepted = accepted;
            LabBootstrap.Instance.Notice = message;
            LabLog.Write("request_reply", "request=" + serial + "; accepted=" + accepted + "; " + message);
        }

        public bool LoadDestination(string scene)
        {
            if (!IsServer || loading) return false;
            if (SceneManager.GetActiveScene().name == scene) return true;
            var status = NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
            LabLog.Write("load_requested", scene + "; status=" + status);
            if (status != SceneEventProgressStatus.Started) return false;
            Destination.Value = new FixedString64Bytes(scene);
            loading = true;
            return true;
        }
    }
}
