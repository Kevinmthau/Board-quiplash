using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TableLaughs
{
    public sealed class HandwritingPaperInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float MinPointDistance = 4f;
        private const int InactivePointerId = int.MinValue;

        private readonly HandwritingAnswer answer = HandwritingAnswer.Blank();

        private RectTransform rectTransform;
        private RectTransform inkRoot;
        private Image paperImage;
        private Color inkColor = new Color(0.05f, 0.06f, 0.07f);
        private float inkThickness = 7f;
        private Action<HandwritingAnswer> onChanged;
        private HandwritingStroke activeStroke;
        private Vector2 lastLocalPoint;
        private int activePointerId = InactivePointerId;
        private bool inputEnabled = true;

        public void Initialize(Color strokeColor, float strokeThickness, Action<HandwritingAnswer> changed)
        {
            rectTransform = transform as RectTransform;
            paperImage = GetComponent<Image>();
            inkColor = strokeColor;
            inkThickness = strokeThickness;
            onChanged = changed;
            EnsureInkRoot();
            SetInputEnabled(inputEnabled);
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (paperImage == null)
            {
                paperImage = GetComponent<Image>();
            }

            if (paperImage != null)
            {
                paperImage.raycastTarget = enabled;
            }
        }

        public HandwritingAnswer GetAnswer()
        {
            return answer.Clone();
        }

        public void SetAnswer(HandwritingAnswer value)
        {
            ClearInkObjects();
            answer.Strokes.Clear();
            answer.Text = value?.Text ?? string.Empty;

            if (value != null)
            {
                for (var i = 0; i < value.Strokes.Count; i++)
                {
                    answer.Strokes.Add(value.Strokes[i].Clone());
                }
            }

            Redraw();
            activeStroke = null;
            activePointerId = InactivePointerId;
        }

        public void Clear()
        {
            SetAnswer(HandwritingAnswer.Blank());
            NotifyChanged();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!inputEnabled || activePointerId != InactivePointerId || !TryGetLocalPoint(eventData, out var localPoint))
            {
                return;
            }

            activePointerId = eventData.pointerId;
            activeStroke = new HandwritingStroke();
            answer.Strokes.Add(activeStroke);
            AddPoint(activeStroke, localPoint);
            CreateDot(localPoint);
            lastLocalPoint = localPoint;
            NotifyChanged();
            eventData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!inputEnabled || eventData.pointerId != activePointerId || activeStroke == null ||
                !TryGetLocalPoint(eventData, out var localPoint))
            {
                return;
            }

            if ((localPoint - lastLocalPoint).sqrMagnitude < MinPointDistance * MinPointDistance)
            {
                return;
            }

            CreateSegment(lastLocalPoint, localPoint);
            AddPoint(activeStroke, localPoint);
            lastLocalPoint = localPoint;
            NotifyChanged();
            eventData.Use();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            activeStroke = null;
            activePointerId = InactivePointerId;
            eventData.Use();
        }

        private void AddPoint(HandwritingStroke stroke, Vector2 localPoint)
        {
            stroke.Points.Add(LocalToNormalized(localPoint));
        }

        private void Redraw()
        {
            EnsureInkRoot();
            for (var strokeIndex = 0; strokeIndex < answer.Strokes.Count; strokeIndex++)
            {
                var stroke = answer.Strokes[strokeIndex];
                if (stroke.Points.Count == 0)
                {
                    continue;
                }

                var previous = NormalizedToLocal(stroke.Points[0]);
                CreateDot(previous);
                for (var pointIndex = 1; pointIndex < stroke.Points.Count; pointIndex++)
                {
                    var current = NormalizedToLocal(stroke.Points[pointIndex]);
                    CreateSegment(previous, current);
                    previous = current;
                }
            }
        }

        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (rectTransform == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out localPoint))
            {
                return false;
            }

            var rect = rectTransform.rect;
            localPoint.x = Mathf.Clamp(localPoint.x, rect.xMin, rect.xMax);
            localPoint.y = Mathf.Clamp(localPoint.y, rect.yMin, rect.yMax);
            return true;
        }

        private Vector2 LocalToNormalized(Vector2 localPoint)
        {
            var rect = rectTransform.rect;
            return new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));
        }

        private Vector2 NormalizedToLocal(Vector2 normalizedPoint)
        {
            var rect = rectTransform.rect;
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalizedPoint.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalizedPoint.y));
        }

        private void CreateDot(Vector2 localPoint)
        {
            var dot = new GameObject("Ink Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(inkRoot, false);
            var dotRect = dot.GetComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = localPoint;
            dotRect.sizeDelta = new Vector2(inkThickness, inkThickness);
            var image = dot.GetComponent<Image>();
            image.color = inkColor;
            image.raycastTarget = false;
        }

        private void CreateSegment(Vector2 start, Vector2 end)
        {
            var delta = end - start;
            var length = delta.magnitude;
            if (length <= 0.01f)
            {
                CreateDot(start);
                return;
            }

            var segment = new GameObject("Ink Segment", typeof(RectTransform), typeof(Image));
            segment.transform.SetParent(inkRoot, false);
            var segmentRect = segment.GetComponent<RectTransform>();
            segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
            segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
            segmentRect.anchoredPosition = (start + end) * 0.5f;
            segmentRect.sizeDelta = new Vector2(length + inkThickness, inkThickness);
            segmentRect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = segment.GetComponent<Image>();
            image.color = inkColor;
            image.raycastTarget = false;
        }

        private void EnsureInkRoot()
        {
            if (inkRoot != null)
            {
                return;
            }

            var inkObject = new GameObject("Ink", typeof(RectTransform));
            inkObject.transform.SetParent(transform, false);
            inkRoot = inkObject.GetComponent<RectTransform>();
            inkRoot.anchorMin = Vector2.zero;
            inkRoot.anchorMax = Vector2.one;
            inkRoot.offsetMin = Vector2.zero;
            inkRoot.offsetMax = Vector2.zero;
        }

        private void ClearInkObjects()
        {
            EnsureInkRoot();
            for (var i = inkRoot.childCount - 1; i >= 0; i--)
            {
                var child = inkRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    child.transform.SetParent(null, false);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void NotifyChanged()
        {
            onChanged?.Invoke(answer.Clone());
        }
    }
}
