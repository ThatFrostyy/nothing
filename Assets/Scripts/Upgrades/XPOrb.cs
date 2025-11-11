using UnityEngine;


namespace FF
{
    public class XPOrb : MonoBehaviour
    {
        [SerializeField] int value = 1;
        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<XPWallet>(out var w))
            {
                w.Add(value); Destroy(gameObject);
            }
        }
    }
}