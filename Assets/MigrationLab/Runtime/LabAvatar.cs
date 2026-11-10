using Unity.Netcode;
using UnityEngine;

namespace MigrationLab
{
    // World coordinates in a shared, identity-scale scene. No full-body IK or accuracy claim.
    public struct TargetPose : INetworkSerializable, System.IEquatable<TargetPose>
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public TargetPose(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Position); s.SerializeValue(ref Rotation); }
        public bool Equals(TargetPose other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation);
    }

    public sealed class LabAvatar : NetworkBehaviour
    {
        public Transform Head;
        public Transform LeftHand;
        public Transform RightHand;
        public Transform Body;
        public LineRenderer LeftArm;
        public LineRenderer RightArm;
        private readonly NetworkVariable<TargetPose> head = new NetworkVariable<TargetPose>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<TargetPose> left = new NetworkVariable<TargetPose>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<TargetPose> right = new NetworkVariable<TargetPose>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> ready = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private float nextSend;
        private bool lastWrong;

        public override void OnNetworkSpawn()
        {
            DontDestroyOnLoad(gameObject);
            Color color = OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? Color.cyan : Color.yellow;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.material.color = color;
            LabLog.Write("avatar_spawn", "owner=" + OwnerClientId + "; local_owner=" + IsOwner);
        }

        private void LateUpdate()
        {
            if (!IsSpawned || LabBootstrap.Instance == null) return;
            var b = LabBootstrap.Instance;
            if (IsOwner && Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + 0.05f; // Demonstration send schedule, not a measured guarantee.
                b.GetLocalTargets(out TargetPose h, out TargetPose l, out TargetPose r);
                head.Value = h; left.Value = l; right.Value = r; ready.Value = true;
            }
            bool wrong = !IsOwner && b.WrongRemoteInputs;
            if (wrong != lastWrong)
            { LabLog.Write("avatar_render_mode", "owner=" + OwnerClientId + "; wrong_remote_inputs=" + wrong); lastWrong = wrong; }
            if (!ready.Value) return;
            TargetPose hp = head.Value, lp = left.Value, rp = right.Value;
            if (wrong) b.GetLocalTargets(out hp, out lp, out rp); // Explicit opt-in fault injection only.
            Head.SetPositionAndRotation(hp.Position, hp.Rotation);
            LeftHand.SetPositionAndRotation(lp.Position, lp.Rotation);
            RightHand.SetPositionAndRotation(rp.Position, rp.Rotation);
            Vector3 center = hp.Position + Vector3.down * 0.55f;
            Body.position = center;
            Body.rotation = Quaternion.Euler(0, hp.Rotation.eulerAngles.y, 0);
            Vector3 shoulder = center + Vector3.up * 0.25f;
            LeftArm.SetPosition(0, shoulder - Body.right * 0.2f); LeftArm.SetPosition(1, lp.Position);
            RightArm.SetPosition(0, shoulder + Body.right * 0.2f); RightArm.SetPosition(1, rp.Position);
            // Hide only the owning participant's head so it does not cover the local camera.
            Head.GetComponent<Renderer>().enabled = !IsOwner;
        }
    }
}
