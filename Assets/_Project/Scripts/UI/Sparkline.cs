using UnityEngine;
using UnityEngine.UIElements;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Dashboard: draws a <see cref="SparkSeries"/> as a line scaled to its own min and max, with a dot on the
    /// newest sample. Redraws only when the series' version moves. Line colour comes from the USS custom
    /// property --spark-color.
    /// </summary>
    [UxmlElement]
    public partial class Sparkline : VisualElement
    {
        private static readonly CustomStyleProperty<Color> LineColorProperty = new CustomStyleProperty<Color>("--spark-color");
        private const float Padding = 3f;

        private SparkSeries _series;
        private int _drawnVersion = -1;
        private Color _lineColor = new Color(0.56f, 0.78f, 0.86f);

        public Sparkline()
        {
            AddToClassList("sparkline");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
            schedule.Execute(CheckForNewSamples).Every(100);
        }

        public SparkSeries Series
        {
            get => _series;
            set
            {
                _series = value;
                _drawnVersion = -1;
                MarkDirtyRepaint();
            }
        }

        private void CheckForNewSamples()
        {
            if (_series != null && _series.Version != _drawnVersion)
                MarkDirtyRepaint();
        }

        private void OnCustomStyle(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(LineColorProperty, out Color color))
            {
                _lineColor = color;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            if (_series == null)
                return;
            _drawnVersion = _series.Version;

            int count = _series.Count;
            Rect rect = contentRect;
            if (count < 2 || rect.width < 4f || rect.height < 4f)
                return;

            float min = _series[0];
            float max = min;
            for (int i = 1; i < count; i++)
            {
                float v = _series[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }
            float range = max - min;
            if (range < 1e-6f)
            {
                min -= 0.5f;
                range = 1f;
            }

            float height = rect.height - 2f * Padding;
            float step = (rect.width - 2f * Padding) / (count - 1);

            Painter2D painter = context.painter2D;
            painter.strokeColor = _lineColor;
            painter.lineWidth = 1.5f;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            Vector2 point = default;
            for (int i = 0; i < count; i++)
            {
                point = new Vector2(
                    rect.xMin + Padding + i * step,
                    rect.yMax - Padding - (_series[i] - min) / range * height);
                if (i == 0)
                    painter.MoveTo(point);
                else
                    painter.LineTo(point);
            }
            painter.Stroke();

            painter.fillColor = _lineColor;
            painter.BeginPath();
            painter.Arc(point, 2.5f, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
        }
    }
}
