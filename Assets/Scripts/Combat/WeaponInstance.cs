using UnityEngine;

namespace FF
{
    public class WeaponInstance
    {
        public Weapon Weapon { get; }
        public GameObject Instance { get; }
        public Transform Muzzle { get; }
        public Transform Eject { get; }
        public GameObjectPool Pool { get; }

        public WeaponInstance(Weapon weapon, GameObject instance, Transform muzzle, Transform eject, GameObjectPool pool)
        {
            Weapon = weapon;
            Instance = instance;
            Muzzle = muzzle;
            Eject = eject;
            Pool = pool;
        }

        public void SetActive(bool active)
        {
            if (Instance)
            {
                Instance.SetActive(active);
            }
        }

        public void Release()
        {
            if (!Instance)
            {
                return;
            }

            if (Pool != null)
            {
                Pool.Release(Instance);
            }
            else
            {
                PoolManager.Release(Instance);
            }
        }
    }
}
