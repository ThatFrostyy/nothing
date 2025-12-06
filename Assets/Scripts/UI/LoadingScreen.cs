using System.Collections;
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
        }

        public static void Show(string message)
        {
            EnsureInstance();
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

        public void Hide()
        {
            if (_instance == null)
            {
                return;
            }

            this.gameObject.SetActive(false);
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
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
                messageText.text = "Loading...";
            }
        }
    }
}
