using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): one portable extinguisher on the training station's stand, worked with two hands. One hand
    /// grips the carry handle (the body's grab); the other pulls the pin out and takes the hose by its nozzle; the
    /// trigger on the hand holding the handle squeezes the lever. It discharges only with the pin out and charge
    /// left. Let go of the handle and it goes back on its hook, full, pin in, hose stowed. Read by FireExtinguisher.
    /// </summary>
    public sealed class ExtinguisherUnit : MonoBehaviour
    {
        [SerializeField] private FireAgent agent = FireAgent.DryChemicalAbc;
        [SerializeField] private XRGrabInteractable body;
        [SerializeField] private XRSimpleInteractable pin;
        [SerializeField] private XRGrabInteractable nozzle;
        [Tooltip("The squeeze lever; it dips while squeezed.")]
        [SerializeField] private Transform lever;
        [Tooltip("The hose as stowed (a mesh), hidden while the nozzle is in a hand.")]
        [SerializeField] private GameObject hoseMesh;
        [Tooltip("The hose while the nozzle is in a hand: a sagging line from the valve to the nozzle.")]
        [SerializeField] private LineRenderer hoseLine;
        [Tooltip("Where the hose leaves the valve.")]
        [SerializeField] private Transform hoseStart;
        [Tooltip("Nozzle length, tip to hose end, in metres.")]
        [SerializeField] private float nozzleLength = 0.07f;
        [SerializeField, Min(0.01f)] private float pinPullMetres = 0.05f;
        [SerializeField] private float leverSqueezeDegrees = 12f;

        [Header("Feedback")]
        [Tooltip("Plays the discharge hiss (made in code) and the pin's click.")]
        [SerializeField] private AudioSource sound;
        [Tooltip("Brightness of the hiss: higher for CO2, lower for water.")]
        [SerializeField, Range(0.05f, 1f)] private float hissBrightness = 0.45f;
        [SerializeField, Range(0f, 1f)] private float squeezeBuzz = 0.22f;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool pinPulled;
        [SerializeField] private bool squeezing;
        [SerializeField] private float charge = 1f;

        private Vector3 _homePosition, _nozzlePosition, _pinPosition;
        private Quaternion _homeRotation, _nozzleRotation, _pinRotation, _leverRotation;
        private Transform _homeParent;
        private IXRSelectInteractor _pinHand;
        private IXRSelectInteractor _handleHand;
        private bool _triggerHeld;
        private AudioClip _hiss, _click;
        private float _nextBuzz;

        public FireAgent Agent => agent;
        public bool Held => body != null && body.isSelected;
        public bool PinPulled => pinPulled;
        public bool HoseHeld => nozzle != null && nozzle.isSelected;
        public bool Squeezing => squeezing;
        public float Charge => charge;
        public Transform NozzleTip => nozzle != null ? nozzle.transform : transform;

        /// <summary>Raised when it is taken from the stand and when it goes back.</summary>
        public event Action<ExtinguisherUnit> HeldChanged;

        private void Awake()
        {
            _homeParent = transform.parent;
            _homePosition = transform.localPosition;
            _homeRotation = transform.localRotation;
            if (nozzle != null)
            {
                _nozzlePosition = nozzle.transform.localPosition;
                _nozzleRotation = nozzle.transform.localRotation;
            }
            if (pin != null)
            {
                _pinPosition = pin.transform.localPosition;
                _pinRotation = pin.transform.localRotation;
            }
            if (lever != null)
                _leverRotation = lever.localRotation;
            if (hoseLine != null)
                hoseLine.enabled = false;
            if (sound != null)
            {
                _hiss = ProceduralAudio.Clip(name + "_Hiss", ProceduralAudio.HissSamples(1.5f, hissBrightness, 0.5f, GetInstanceID()));
                _click = ProceduralAudio.Clip(name + "_PinClick", ProceduralAudio.ClickSamples(2600f, 0.12f, 0.6f));
                sound.clip = _hiss;
                sound.loop = true;
                sound.playOnAwake = false;
            }
        }

        private void OnDestroy()
        {
            if (_hiss != null)
                Destroy(_hiss);
            if (_click != null)
                Destroy(_click);
        }

        private void OnEnable()
        {
            if (body != null)
            {
                body.selectEntered.AddListener(OnTaken);
                body.selectExited.AddListener(OnLetGo);
                body.activated.AddListener(OnSqueeze);
                body.deactivated.AddListener(OnRelease);
            }
            if (pin != null)
            {
                pin.selectEntered.AddListener(OnPinGrabbed);
                pin.selectExited.AddListener(OnPinLetGo);
            }
            if (nozzle != null)
                nozzle.selectExited.AddListener(OnNozzleLetGo);
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.selectEntered.RemoveListener(OnTaken);
                body.selectExited.RemoveListener(OnLetGo);
                body.activated.RemoveListener(OnSqueeze);
                body.deactivated.RemoveListener(OnRelease);
            }
            if (pin != null)
            {
                pin.selectEntered.RemoveListener(OnPinGrabbed);
                pin.selectExited.RemoveListener(OnPinLetGo);
            }
            if (nozzle != null)
                nozzle.selectExited.RemoveListener(OnNozzleLetGo);
        }

        private void Update()
        {
            // The pin comes out once the hand holding it has pulled it this far from its seat.
            if (!pinPulled && _pinHand != null && pin != null)
            {
                Vector3 seat = pin.transform.parent.TransformPoint(_pinPosition);
                Vector3 hand = _pinHand.GetAttachTransform(pin).position;
                if (Vector3.Distance(seat, hand) >= pinPullMetres)
                    PullPin();
            }

            bool wants = _triggerHeld && Held && pinPulled && charge > 0f;
            if (wants)
            {
                double capacity = ExtinguisherRules.CapacitySeconds(agent);
                charge = capacity > 0d ? Mathf.Max(0f, charge - (float)(Time.deltaTime / capacity)) : 0f;
                if (charge <= 0f)
                    Empty();
            }
            bool was = squeezing;
            squeezing = wants && charge > 0f;
            if (squeezing != was)
                Hiss(squeezing);
            // A steady buzz in the handle hand while it discharges.
            if (squeezing && Time.unscaledTime >= _nextBuzz)
            {
                _nextBuzz = Time.unscaledTime + 0.1f;
                Buzz(_handleHand, squeezeBuzz, 0.12f);
            }
            if (lever != null)
                lever.localRotation = squeezing ? _leverRotation * Quaternion.Euler(leverSqueezeDegrees, 0f, 0f) : _leverRotation;
        }

        private void LateUpdate()
        {
            bool hoseOut = HoseHeld;
            if (hoseMesh != null && hoseMesh.activeSelf == hoseOut)
                hoseMesh.SetActive(!hoseOut);
            if (hoseLine == null || hoseStart == null)
                return;
            hoseLine.enabled = hoseOut;
            if (!hoseOut)
                return;
            // A quadratic curve that sags between the valve and the back of the nozzle.
            Vector3 a = hoseStart.position;
            Vector3 b = nozzle.transform.position - nozzle.transform.forward * nozzleLength;
            Vector3 mid = (a + b) * 0.5f + Vector3.down * Mathf.Min(0.35f, 0.25f + Vector3.Distance(a, b) * 0.2f);
            int count = hoseLine.positionCount;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                float u = 1f - t;
                hoseLine.SetPosition(i, u * u * a + 2f * u * t * mid + t * t * b);
            }
        }

        /// <summary>Back on its hook, full, pin in, hose stowed; drops it from any hand holding it.</summary>
        public void PutBack()
        {
            Cancel(nozzle);
            Cancel(body);
            ReturnHome();
        }

        private void PullPin()
        {
            pinPulled = true;
            // A sharp tick in the hand that pulled it, and the click.
            Buzz(_pinHand, 0.7f, 0.06f);
            if (sound != null && _click != null)
                sound.PlayOneShot(_click);
            _pinHand = null;
            // Out and away: the trainee has it in hand and drops it; it is not needed again until the unit is refilled.
            pin.gameObject.SetActive(false);
        }

        private void OnTaken(SelectEnterEventArgs args)
        {
            _handleHand = args.interactorObject;
            HeldChanged?.Invoke(this);
        }

        private void OnLetGo(SelectExitEventArgs args)
        {
            _handleHand = null;
            _triggerHeld = false;
            Cancel(nozzle);
            ReturnHome();
            HeldChanged?.Invoke(this);
        }

        private void OnSqueeze(ActivateEventArgs args) => _triggerHeld = true;
        private void OnRelease(DeactivateEventArgs args) => _triggerHeld = false;
        private void OnPinGrabbed(SelectEnterEventArgs args) => _pinHand = args.interactorObject;
        private void OnPinLetGo(SelectExitEventArgs args) => _pinHand = null;

        private void OnNozzleLetGo(SelectExitEventArgs args)
        {
            // Back on its clip on the body (XRI puts it back under its parent; the pose is ours to restore).
            nozzle.transform.SetLocalPositionAndRotation(_nozzlePosition, _nozzleRotation);
        }

        private void ReturnHome()
        {
            if (transform.parent != _homeParent)
                transform.SetParent(_homeParent, false);
            transform.SetLocalPositionAndRotation(_homePosition, _homeRotation);
            if (nozzle != null)
                nozzle.transform.SetLocalPositionAndRotation(_nozzlePosition, _nozzleRotation);
            if (pin != null)
            {
                pin.gameObject.SetActive(true);
                pin.transform.SetLocalPositionAndRotation(_pinPosition, _pinRotation);
            }
            pinPulled = false;
            if (squeezing)
                Hiss(false);
            squeezing = false;
            _triggerHeld = false;
            charge = 1f;
            if (hoseLine != null)
                hoseLine.enabled = false;
            if (hoseMesh != null)
                hoseMesh.SetActive(true);
        }

        // Out of charge: two short buzzes in the handle hand.
        private void Empty()
        {
            Buzz(_handleHand, 0.6f, 0.08f);
            Invoke(nameof(SecondEmptyBuzz), 0.18f);
        }

        private void SecondEmptyBuzz() => Buzz(_handleHand, 0.6f, 0.08f);

        private void Hiss(bool on)
        {
            if (sound == null || _hiss == null)
                return;
            if (on)
            {
                sound.clip = _hiss;
                sound.loop = true;
                sound.Play();
            }
            else
            {
                sound.Stop();
            }
        }

        // The controller's haptics, found from the interactor holding the part (XRI's HapticImpulsePlayer on the rig).
        private static void Buzz(IXRSelectInteractor hand, float amplitude, float seconds)
        {
            if (hand == null || hand.transform == null)
                return;
            var player = hand.transform.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics.HapticImpulsePlayer>();
            if (player == null)
                player = hand.transform.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics.HapticImpulsePlayer>();
            if (player != null)
                player.SendHapticImpulse(amplitude, seconds);
        }

        private static void Cancel(XRBaseInteractable interactable)
        {
            if (interactable != null && interactable.isSelected && interactable.interactionManager != null)
                interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);
        }
    }
}
