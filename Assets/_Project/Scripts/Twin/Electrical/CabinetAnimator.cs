using System;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Electrical safety (M19): shows the drive cabinet's state in 3D from its signals only: the door swings on its
    /// hinge (cell/cabinet/door_open), Q1's through-door handle turns between ON and OFF (cell/cabinet/disconnect),
    /// and each VFD's DC bus charge LED glows while its bus is above the safe level (cell/cabinet/dc_bus_v for U1;
    /// U2 follows the main bus and decays on the same schedule, so it shares the signal).
    /// </summary>
    public sealed class CabinetAnimator : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CellContext context;
        [SerializeField] private Transform door;
        [Tooltip("Door angle when open, about the hinge's local Y. The hinge is on the door's left as you face it (+X), so a positive angle swings it out towards you.")]
        [SerializeField] private float doorOpenDegrees = 105f;
        [SerializeField, Min(1f)] private float doorDegreesPerSecond = 150f;
        [SerializeField] private Transform handle;
        [Tooltip("Handle angle in OFF about its local Z; ON is 0 (pointing up). -90 turns it to 9 o'clock as you face the door, where the plate says OFF.")]
        [SerializeField] private float handleOffDegrees = -90f;
        [SerializeField] private Renderer[] dcLeds = Array.Empty<Renderer>();
        [SerializeField] private Color ledColor = new Color(1f, 0.15f, 0.05f);
        [SerializeField, Min(0f)] private float safeDcVolts = 50f;
        [SerializeField, Min(0f)] private float litEmission = 2f;

        private MaterialPropertyBlock _block;
        private IDisposable[] _subscriptions = Array.Empty<IDisposable>();
        private bool _doorOpen;
        private bool _disconnectOpen;
        private bool _ledLit;
        private bool _ledShown = true;
        private float _doorAngle;
        private Quaternion _doorClosed = Quaternion.identity;
        private Quaternion _handleOn = Quaternion.identity;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (door != null)
                _doorClosed = door.localRotation;
            if (handle != null)
                _handleOn = handle.localRotation;
        }

        private void OnEnable()
        {
            SignalBus bus = context.Bus;
            _subscriptions = new[]
            {
                bus.Subscribe(CellTopics.CabinetDoorOpen.ToString(), m => _doorOpen = m.Value.AsNumber != 0d),
                bus.Subscribe(CellTopics.CabinetDisconnect.ToString(), m => ShowHandle(m.Value.AsNumber != 0d)),
                bus.Subscribe(CellTopics.CabinetDcBusVolts.ToString(), m => _ledLit = m.Value.AsNumber >= safeDcVolts),
            };
            if (bus.TryGetLast(CellTopics.CabinetDoorOpen, out SignalMessage d))
                _doorOpen = d.Value.AsNumber != 0d;
            if (bus.TryGetLast(CellTopics.CabinetDisconnect, out SignalMessage q))
                ShowHandle(q.Value.AsNumber != 0d);
            _ledLit = !bus.TryGetLast(CellTopics.CabinetDcBusVolts, out SignalMessage v) || v.Value.AsNumber >= safeDcVolts;
        }

        private void OnDisable()
        {
            foreach (IDisposable s in _subscriptions)
                s?.Dispose();
            _subscriptions = Array.Empty<IDisposable>();
        }

        private void Update()
        {
            float target = _doorOpen ? doorOpenDegrees : 0f;
            if (door != null && !Mathf.Approximately(_doorAngle, target))
            {
                _doorAngle = Mathf.MoveTowards(_doorAngle, target, doorDegreesPerSecond * Time.deltaTime);
                door.localRotation = _doorClosed * Quaternion.Euler(0f, _doorAngle, 0f);
            }
            if (_ledLit != _ledShown)
                ShowLeds(_ledLit);
        }

        private void ShowHandle(bool off)
        {
            _disconnectOpen = off;
            if (handle != null)
                handle.localRotation = _handleOn * Quaternion.Euler(0f, 0f, _disconnectOpen ? handleOffDegrees : 0f);
        }

        private void ShowLeds(bool lit)
        {
            _block.Clear();
            _block.SetColor(BaseColorId, lit ? ledColor : ledColor * 0.25f);
            _block.SetColor(EmissionColorId, lit ? ledColor * litEmission : Color.black);
            foreach (Renderer led in dcLeds)
            {
                if (led != null)
                    led.SetPropertyBlock(_block);
            }
            _ledShown = lit;
        }
    }
}
