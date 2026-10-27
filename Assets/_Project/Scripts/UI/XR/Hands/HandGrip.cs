using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): how a VR hand holds this prop. The fingers take these curls while it is held; on a fixed part
    /// (the isolator's handle, the air valve's lever) the hand also leaves the controller and stays on the anchor, a
    /// grip frame (HandPoseMath.GripInPalm) set for the right hand, turned for the left. Props that move with the
    /// hand (padlock, tools) need no anchor: their grab attach puts them in the palm.
    /// </summary>
    public sealed class HandGrip : MonoBehaviour
    {
        [SerializeField] private FingerCurls curls = FingerCurls.Handle;
        [Tooltip("Optional: where the hand closes on a fixed part, as a right-hand grip frame (+Z thumbward along the bar, +X into the palm).")]
        [SerializeField] private Transform anchor;

        public FingerCurls Curls => curls;
        public Transform Anchor => anchor;
    }
}
