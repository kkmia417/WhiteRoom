using UnityEngine;
using UnityEngine.EventSystems;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Lightweight pointer/controller feedback for a pooled dialogue choice.
    /// It never owns selection or navigation; the Button remains authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NovelChoiceMotionFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private const float HighlightedScale = 1.035f;
        private const float PressedScale = 0.985f;
        private const float Response = 18f;

        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        private bool _hovered;
        private bool _selected;
        private bool _pressed;

        public float DesiredScaleMultiplier
        {
            get
            {
                if (_pressed)
                    return PressedScale;
                return _hovered || _selected ? HighlightedScale : 1f;
            }
        }

        public void Configure()
        {
            if (_rect == null)
                _rect = transform as RectTransform;
            if (_rect != null)
                _baseScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            _pressed = false;
        }

        private void OnEnable()
        {
            Configure();
            SnapToRest();
        }

        private void OnDisable()
        {
            _hovered = false;
            _selected = false;
            _pressed = false;
            SnapToRest();
        }

        private void Update()
        {
            if (_rect == null)
                return;

            var target = _baseScale * DesiredScaleMultiplier;
            var blend = 1f - Mathf.Exp(-Response * Time.unscaledDeltaTime);
            _rect.localScale = Vector3.LerpUnclamped(_rect.localScale, target, blend);
        }

        private void SnapToRest()
        {
            if (_rect != null)
                _rect.localScale = _baseScale;
        }
    }
}
