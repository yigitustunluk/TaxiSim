using UnityEngine;

// Satın alınmış yükseltmeleri taksiye uygular. Taxi objesine eklenir.
//
// Önemli: Taksinin Inspector'daki değerlerini "temel değer" olarak bir kez saklıyoruz.
// Her uygulamada temel değerden başlayıp yeniden çarpıyoruz. Böylece oyun ortasında
// Apply() kaç kez çağrılırsa çağrılsın değerler üst üste binip katlanmaz.
[RequireComponent(typeof(TaxiController))]
public class TaxiUpgrades : MonoBehaviour
{
    public UpgradeDefinition[] upgrades;

    [Tooltip("Motor yükseltmesinin ivmeye etkisi, azami hıza etkisinin bu oranı kadar. " +
             "0.4 = azami hız %15 artarken ivme %6 artar. Böylece yükseltilmiş taksi kontrolsüz hızlanmaz.")]
    [Range(0f, 1f)] public float engineAccelerationShare = 0.4f;

    TaxiController taxi;
    float baseAcceleration, baseMaxSpeed, baseBrakeForce, baseGrip;

    void Awake()
    {
        taxi = GetComponent<TaxiController>();
        baseAcceleration = taxi.acceleration;
        baseMaxSpeed = taxi.maxSpeed;
        baseBrakeForce = taxi.brakeForce;
        baseGrip = taxi.gripStrength;
        Apply();
    }

    // Garaj, her satın almadan sonra bunu çağırır: yükseltme anında etki eder
    public void Apply()
    {
        // Önce temel değerlere dön
        taxi.acceleration = baseAcceleration;
        taxi.maxSpeed = baseMaxSpeed;
        taxi.brakeForce = baseBrakeForce;
        taxi.gripStrength = baseGrip;

        foreach (UpgradeDefinition u in upgrades)
        {
            if (u == null) continue;
            float m = u.Multiplier;

            switch (u.stat)
            {
                case UpgradeStat.Engine:
                    taxi.maxSpeed *= m;
                    taxi.acceleration *= 1f + (m - 1f) * engineAccelerationShare;
                    break;
                case UpgradeStat.Brakes:
                    taxi.brakeForce *= m;
                    break;
                case UpgradeStat.Grip:
                    taxi.gripStrength *= m;
                    break;
            }
        }
    }
}
