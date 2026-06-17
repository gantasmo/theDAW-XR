using UnityEngine;

namespace HologramVFXDemo
{
    public class ObjectSelectEnable : MonoBehaviour
    {
        [Tooltip("List of GameObjects to cycle through.")]
        public GameObject[] objects;

        private int currentIndex = 0;

        void Start()
        {
            // Disable all at the start except the first one
            UpdateSelection();
        }

        void Update()
        {
            if (objects.Length == 0) return;

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                currentIndex = (currentIndex + 1) % objects.Length;
                UpdateSelection();
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                currentIndex = (currentIndex - 1 + objects.Length) % objects.Length;
                UpdateSelection();
            }
        }

        void UpdateSelection()
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    objects[i].SetActive(i == currentIndex);
            }

            Debug.Log($"Selected: {objects[currentIndex].name}");
        }
    }
}