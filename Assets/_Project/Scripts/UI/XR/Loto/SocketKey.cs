using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Training (M12): marks what a grabbable lockout prop is, for the sockets that take it ("padlock", "tool").</summary>
    public sealed class SocketKey : MonoBehaviour
    {
        [SerializeField] private string key = string.Empty;

        public string Key => key;
    }
}
