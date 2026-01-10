using UnityEngine;

namespace FF
{
    public class CrateScrap : MonoBehaviour
    {
        [SerializeField] private int minScrap = 5;
        [SerializeField] private int maxScrap = 15;
        
        public void DropScrap()
        {
            int amount = Random.Range(minScrap, maxScrap + 1);
            if (ScrapManager.Instance != null)
            {
                ScrapManager.Instance.AddScrap(amount);
                Debug.Log($"Crate dropped {amount} scrap!");
                // Optional: Instantiate a floating text or UI effect here
            }
        }
    }
}
