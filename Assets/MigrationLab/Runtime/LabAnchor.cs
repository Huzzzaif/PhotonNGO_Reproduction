using UnityEngine;

namespace MigrationLab
{
    public sealed class LabAnchor : MonoBehaviour
    {
        public Transform[] Stops;
        public Transform GetStop(int index)
        {
            if (Stops == null || index < 0 || index >= Stops.Length) return null;
            return Stops[index];
        }
    }
}
