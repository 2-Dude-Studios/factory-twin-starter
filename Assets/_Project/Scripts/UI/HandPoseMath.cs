using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>Training (M12): how far each finger of a VR hand is curled, 0 open to 1 a closed fist.</summary>
    [Serializable]
    public struct FingerCurls
    {
        [Range(0f, 1f)] public float thumb;
        [Range(0f, 1f)] public float index;
        [Range(0f, 1f)] public float middle;
        [Range(0f, 1f)] public float ring;
        [Range(0f, 1f)] public float little;

        public FingerCurls(float thumb, float index, float middle, float ring, float little)
        {
            this.thumb = thumb;
            this.index = index;
            this.middle = middle;
            this.ring = ring;
            this.little = little;
        }

        /// <summary>A hand at rest on a controller: fingers loosely round the handle, the index a little straighter.</summary>
        public static FingerCurls Relaxed => new FingerCurls(0.3f, 0.25f, 0.35f, 0.38f, 0.4f);

        /// <summary>A power grip round a bar or a handle about 3 cm across.</summary>
        public static FingerCurls Handle => new FingerCurls(0.55f, 0.7f, 0.75f, 0.78f, 0.8f);

        public float this[int finger]
        {
            get
            {
                switch (finger)
                {
                    case 0: return thumb;
                    case 1: return index;
                    case 2: return middle;
                    case 3: return ring;
                    default: return little;
                }
            }
        }
    }

    /// <summary>
    /// Training (M12): the pose rules for the VR hands, kept apart from the components so EditMode tests reach them.
    /// The controller hands curl with the grip and trigger, or take a held prop's grip; both kinds of hand put a held
    /// prop at the same place in the palm (the grip frame).
    /// </summary>
    public static class HandPoseMath
    {
        public const int Fingers = 5;

        /// <summary>
        /// The curls for a controller's grip and trigger (0 to 1): the trigger bends the index finger, the grip the
        /// other three, and the thumb comes down with either. Released, the hand rests slightly bent.
        /// </summary>
        public static FingerCurls FromInput(float grip, float trigger)
        {
            grip = Mathf.Clamp01(grip);
            trigger = Mathf.Clamp01(trigger);
            FingerCurls rest = FingerCurls.Relaxed;
            float thumb = Mathf.Max(rest.thumb, 0.6f * Mathf.Max(grip, trigger));
            float index = Mathf.Lerp(rest.index, 0.85f, trigger);
            return new FingerCurls(thumb, index, Mathf.Lerp(rest.middle, 0.95f, grip), Mathf.Lerp(rest.ring, 0.95f, grip),
                Mathf.Lerp(rest.little, 0.95f, grip));
        }

        /// <summary>Each finger moved toward its target by at most maxDelta.</summary>
        public static FingerCurls MoveTowards(FingerCurls current, FingerCurls target, float maxDelta)
        {
            return new FingerCurls(
                Mathf.MoveTowards(current.thumb, target.thumb, maxDelta),
                Mathf.MoveTowards(current.index, target.index, maxDelta),
                Mathf.MoveTowards(current.middle, target.middle, maxDelta),
                Mathf.MoveTowards(current.ring, target.ring, maxDelta),
                Mathf.MoveTowards(current.little, target.little, maxDelta));
        }

        /// <summary>
        /// The grip frame in the palm joint's frame (XR Hands axes: +Z toward the fingers, +Y the back of the hand):
        /// the centre of a closed fist, 2.5 cm in front of the palm. Like OpenXR's grip pose, +Z runs through the fist
        /// from the little finger to the thumb and +X is the palm's normal, away from the palm on the left hand and into
        /// it on the right. A controller's own pose is its grip pose, so a hand drawn this way round a controller holds
        /// it, and a prop whose grip child matches this frame sits in the palm of either kind of hand.
        /// </summary>
        public static Pose GripInPalm(bool left)
        {
            Vector3 thumbward = left ? Vector3.right : Vector3.left;
            Vector3 palmNormal = left ? Vector3.down : Vector3.up;
            Vector3 up = Vector3.Cross(thumbward, palmNormal);
            return new Pose(new Vector3(0f, -0.025f, 0.02f), Quaternion.LookRotation(thumbward, up));
        }

        /// <summary>
        /// A grip frame set on a fixed handle for the right hand, turned for the left: half a turn about the fist's
        /// axis, so the left palm faces the handle from the same side.
        /// </summary>
        public static Quaternion AnchorFor(bool left, Quaternion rightHandAnchor)
        {
            return left ? rightHandAnchor * Quaternion.Euler(0f, 0f, 180f) : rightHandAnchor;
        }

        /// <summary>The bend of each finger joint at a full curl, in degrees, from the knuckle out (thumb first).</summary>
        public static float MaxBend(int finger, int joint)
        {
            if (finger == 0)
                return joint == 0 ? 20f : joint == 1 ? 35f : 55f;
            return joint == 0 ? 75f : joint == 1 ? 95f : 65f;
        }

        /// <summary>Inverse of a rigid pose.</summary>
        public static Pose Inverse(Pose pose)
        {
            Quaternion inverse = Quaternion.Inverse(pose.rotation);
            return new Pose(inverse * -pose.position, inverse);
        }

        /// <summary>The pose b expressed in a's parent space when b is given in a's space (a * b).</summary>
        public static Pose Multiply(Pose a, Pose b)
        {
            return new Pose(a.position + a.rotation * b.position, a.rotation * b.rotation);
        }
    }
}
