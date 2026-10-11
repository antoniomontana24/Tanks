using System;
using UnityEngine;

namespace NeonPrism
{
    public class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 1;
        [SerializeField] private GameObject deathEffectPrefab;

        public int Current { get; private set; }
        public bool IsDead { get; private set; }
        public event Action<Health> Died;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            Current -= amount;
            if (Current <= 0)
            {
                Current = 0;
                Die();
            }
        }

        private void Die()
        {
            IsDead = true;
            if (deathEffectPrefab != null)
            {
                Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            }
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
