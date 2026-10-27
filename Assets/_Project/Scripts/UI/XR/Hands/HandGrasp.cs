using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training (M12): the select input of a tracked hand, read from its joints: a fist (middle and ring fingertips
    /// near the palm) or a pinch (thumb and index tips together) holds; opening the hand lets go. Works with any
    /// runtime that sends hand joints (XR_EXT_hand_tracking), with no hand interaction profile needed.
    /// </summary>
    public sealed class HandGrasp : MonoBehaviour, IXRInputButtonReader
    {
        [SerializeField] private XRHandTrackingEvents trackingEvents;
        [Tooltip("Fingertip to palm distance (m) under which a finger counts as closed.")]
        [SerializeField] private float fistDistance = 0.065f;
        [Tooltip("Thumb tip to index tip distance (m) under which the hand pinches.")]
        [SerializeField] private float pinchDistance = 0.02f;
        [Tooltip("Extra distance before a closed hand counts as open again, so the grasp does not flicker.")]
        [SerializeField] private float release = 0.015f;

        private bool _held;
        private int _performedFrame = -1;
        private int _completedFrame = -1;

        private void OnEnable()
        {
            if (trackingEvents == null)
                return;
            trackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
            trackingEvents.trackingLost.AddListener(OnTrackingLost);
        }

        private void OnDisable()
        {
            if (trackingEvents != null)
            {
                trackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
                trackingEvents.trackingLost.RemoveListener(OnTrackingLost);
            }
            Set(false);
        }

        private void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            XRHand hand = args.hand;
            if (!Pose(hand, XRHandJointID.Palm, out Vector3 palm) || !Pose(hand, XRHandJointID.MiddleTip, out Vector3 middle)
                || !Pose(hand, XRHandJointID.RingTip, out Vector3 ring) || !Pose(hand, XRHandJointID.ThumbTip, out Vector3 thumb)
                || !Pose(hand, XRHandJointID.IndexTip, out Vector3 index))
                return;
            float slack = _held ? release : 0f;
            bool fist = Vector3.Distance(middle, palm) < fistDistance + slack && Vector3.Distance(ring, palm) < fistDistance + slack;
            bool pinch = Vector3.Distance(thumb, index) < pinchDistance + slack;
            Set(fist || pinch);
        }

        private void OnTrackingLost() => Set(false);

        private void Set(bool held)
        {
            if (held == _held)
                return;
            _held = held;
            if (held)
                _performedFrame = Time.frameCount;
            else
                _completedFrame = Time.frameCount;
        }

        private static bool Pose(XRHand hand, XRHandJointID id, out Vector3 position)
        {
            position = default;
            if (!hand.GetJoint(id).TryGetPose(out UnityEngine.Pose pose))
                return false;
            position = pose.position;
            return true;
        }

        public bool ReadIsPerformed() => _held;

        public bool ReadWasPerformedThisFrame() => _performedFrame == Time.frameCount;

        public bool ReadWasCompletedThisFrame() => _completedFrame == Time.frameCount;

        public float ReadValue() => _held ? 1f : 0f;

        public bool TryReadValue(out float value)
        {
            value = ReadValue();
            return _held;
        }
    }
}
