using UnityEngine;
using UnityEngine.UI;

namespace TableLaughs
{
    public sealed class ConfettiPiece : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Image image;
        private Vector2 velocity;
        private float spin;
        private float age;
        private float lifetime;

        public void Initialize(Vector2 initialVelocity)
        {
            rectTransform = GetComponent<RectTransform>();
            image = GetComponent<Image>();
            velocity = initialVelocity;
            spin = Random.Range(-300f, 300f);
            lifetime = Random.Range(1.4f, 2.4f);
        }

        private void Update()
        {
            if (rectTransform == null)
            {
                return;
            }

            age += Time.deltaTime;
            velocity += new Vector2(0f, -720f) * Time.deltaTime;
            rectTransform.anchoredPosition += velocity * Time.deltaTime;
            rectTransform.Rotate(0f, 0f, spin * Time.deltaTime);

            if (image != null)
            {
                var color = image.color;
                color.a = Mathf.Clamp01(1f - age / lifetime);
                image.color = color;
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
