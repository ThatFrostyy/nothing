using UnityEngine;

namespace FF
{
    public class WeaponInstance
    {
        public Weapon Weapon { get; }
        public GameObject Instance { get; }
        public Transform Muzzle { get; }
        public Transform Eject { get; }

        public WeaponInstance(Weapon weapon, GameObject instance, Transform muzzle, Transform eject)
        {
            Weapon = weapon;
            Instance = instance;
            Muzzle = muzzle;
            Eject = eject;
        }

        public void SetActive(bool active)
        {
            if (Instance)
            {
                Instance.SetActive(active);
            }
        }
    }
}
