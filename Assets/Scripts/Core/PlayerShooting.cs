using System;
using UnityEngine;

namespace NeonPrism
{
    /// <summary>
    /// Глобальный «звук выстрела» игрока. TankShooting вызывает NotifyShot при каждом успешном выстреле,
    /// враги подписываются на OnShoot.
    /// </summary>
    public static class PlayerShooting
    {
        public static event Action<Vector2> OnShoot;

        public static void NotifyShot(Vector2 playerPosition)
        {
            OnShoot?.Invoke(playerPosition);
        }

        // Если в Project Settings отключена перезагрузка домена — сбрасываем статику при входе в Play Mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnShoot = null;
        }
    }
}
