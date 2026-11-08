using UnityEngine;

// Hangi özelliği yükseltiyor?
public enum UpgradeStat
{
    Engine,   // Motor: ivme ve azami hız
    Brakes,   // Fren gücü
    Grip      // Lastik: yol tutuş
}

// Bir yükseltmenin TANIMI. Bu bir "ScriptableObject": sahnedeki bir obje değil,
// Project penceresinde duran bir VERİ DOSYASI.
//
// Neden böyle? Yükseltmenin adı, fiyatları ve etkisi koda gömülü olursa her değişiklikte
// kodu açmak gerekir. Veri dosyası olunca Inspector'dan değiştirirsin, yeni bir yükseltme
// eklemek için de kod yazmazsın, sadece yeni bir dosya oluşturursun.
//
// Aşağıdaki satır, Project'te sağ tık → Create menüsüne "TaxiSim → Upgrade" seçeneğini ekler.
[CreateAssetMenu(menuName = "TaxiSim/Upgrade", fileName = "NewUpgrade")]
public class UpgradeDefinition : ScriptableObject
{
    public string id = "engine";              // Kayıtta kullanılan benzersiz ad (değiştirme!)
    public string displayName = "Motor";      // Ekranda görünen ad
    [TextArea] public string description = "Daha hızlı ivmelenme ve azami hız.";
    public UpgradeStat stat = UpgradeStat.Engine;

    public int[] costs = { 100, 250, 500 };   // Her seviyenin fiyatı. Eleman sayısı = azami seviye
    public float bonusPerLevel = 0.15f;       // Seviye başına artış (0.15 = %15)

    public int MaxLevel => costs.Length;
    public int CurrentLevel => SaveData.GetLevel(id);
    public bool IsMaxed => CurrentLevel >= MaxLevel;
    public int NextCost => IsMaxed ? 0 : costs[CurrentLevel];

    // Taksinin değerleri bu sayıyla çarpılır. Seviye 2'de: 1 + 0.15 x 2 = 1.30
    public float Multiplier => 1f + bonusPerLevel * CurrentLevel;

    public bool CanBuy => !IsMaxed && SaveData.Money >= NextCost;

    // Para yetiyorsa bir seviye satın alır. Başarılıysa true döner.
    public bool TryBuy()
    {
        if (!CanBuy) return false;
        SaveData.Money -= NextCost;
        SaveData.SetLevel(id, CurrentLevel + 1);
        return true;
    }

    // TEST İÇİN: Inspector'da bu dosyanın ⋮ menüsünde görünür.
    [ContextMenu("Test: Satın al")]
    void TestBuy()
    {
        Debug.Log(TryBuy()
            ? displayName + " seviye " + CurrentLevel + " oldu. Kalan para: " + SaveData.Money
            : displayName + " alınamadı (para yetmiyor ya da seviye dolu).");
    }

    [ContextMenu("Test: Seviyeyi sıfırla")]
    void TestReset()
    {
        SaveData.SetLevel(id, 0);
        Debug.Log(displayName + " seviyesi sıfırlandı.");
    }
}
