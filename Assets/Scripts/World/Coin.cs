using UnityEngine;

namespace SkiGame
{
    /// <summary>A spinning collectible coin (trigger on layer "Ignore Raycast").</summary>
    public class Coin : MonoBehaviour
    {
        [SerializeField] private int value = 1;
        [SerializeField] private float spinSpeed = 200f;
        [SerializeField] private Transform spinner;

        public int Value => value;
        public bool Collected { get; private set; }

        public void Configure(int coinValue, Transform spinningPart)
        {
            value = coinValue;
            spinner = spinningPart;
        }

        private void Update()
        {
            if (spinner != null) spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        public void Collect()
        {
            Collected = true;
            gameObject.SetActive(false);
        }
    }
}
