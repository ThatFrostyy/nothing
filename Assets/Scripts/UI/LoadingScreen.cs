using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FF
{
    public class LoadingScreen : MonoBehaviour
    {
        private static LoadingScreen _instance;

        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        public static void Show(string message)
        {
            EnsureInstance();
            if (_instance == null)
            {
                return;
            }

            _instance.ShowInternal(message);
        }

        public static void UpdateMessage(string message)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.UpdateMessageInternal(message);
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            _instance = FindAnyObjectByType<LoadingScreen>();
            if (_instance != null)
            {
                return;
            }

            var prefab = Resources.Load<LoadingScreen>("Prefabs/Loading");
            if (prefab)
            {
                _instance = Instantiate(prefab);
            }
        }

        private void ShowInternal(string message)
        {
            UpdateMessageInternal(message);
            gameObject.SetActive(true);
        }

        private void UpdateMessageInternal(string message)
        {
            if (messageText)
            {
                messageText.text = message;
            }
        }

        public static void Hide()
        {
            if (_instance == null)
            {
                return;
            }

            _instance.gameObject.SetActive(false);
        }
    }
}
