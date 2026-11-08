using UnityEngine;

// Satın alınmış yükseltmeleri taksiye uygular. Taxi objesine eklenir.
// Oyun sahnesi her yüklendiğinde taksinin değerleri Inspector'daki temel değerlerden başlar,
// bu script de onları yükseltme seviyelerine göre çarpar.
[RequireComponent(typeof(TaxiController))]
public class TaxiUpgrades : MonoBehaviour
{
    public UpgradeDefinition[] upgrades;

    void Awake()
    {
        TaxiController taxi = GetComponent<TaxiController>();

        foreach (UpgradeDefinition u in upgrades)
        {
            if (u == null) continue;
            float m = u.Multiplier;

            // switch: "stat" hangi değerse o bloğa gir
            switch (u.stat)
            {
                case UpgradeStat.Engine:
                    taxi.acceleration *= m;
                    taxi.maxSpeed *= m;
                    break;
                case UpgradeStat.Brakes:
                    taxi.brakeForce *= m;
                    break;
                case UpgradeStat.Grip:
                    taxi.gripStrength *= m;
                    break;
            }

            if (u.CurrentLevel > 0)
                Debug.Log(u.displayName + " seviye " + u.CurrentLevel + " uygulandı (x" + m.ToString("0.00") + ")");
        }
    }
}
