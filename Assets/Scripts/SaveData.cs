using UnityEngine;

// Oyunun kalıcı verileri: para ve yükseltme seviyeleri.
//
// "static" sınıf: Hiçbir objeye eklenmez, sahnede durmaz. Her script doğrudan
// SaveData.Money diye erişebilir. Sahne değişse de kaybolmaz.
//
// PlayerPrefs: Unity'nin küçük verileri kalıcı saklama yolu. Windows'ta kayıt defterine
// (registry) yazılır, oyun kapanıp açılsa da durur.
// Not: Editörde ve build'de oynanan oyunların kayıtları birbirinden ayrıdır.
public static class SaveData
{
    const string MoneyKey = "money";
    const string UpgradePrefix = "upgrade_";

    // Kasadaki toplam para
    public static float Money
    {
        get => PlayerPrefs.GetFloat(MoneyKey, 0f);   // Kayıt yoksa 0
        set
        {
            PlayerPrefs.SetFloat(MoneyKey, value);
            PlayerPrefs.Save();                       // Hemen diske yaz (oyun çökerse kaybolmasın)
        }
    }

    // Bir yükseltmenin seviyesi (örneğin "motor" → 2). Hiç alınmadıysa 0.
    public static int GetLevel(string upgradeId)
    {
        return PlayerPrefs.GetInt(UpgradePrefix + upgradeId, 0);
    }

    public static void SetLevel(string upgradeId, int level)
    {
        PlayerPrefs.SetInt(UpgradePrefix + upgradeId, level);
        PlayerPrefs.Save();
    }

    // Bütün ilerlemeyi sil (test için ya da "Yeni Oyun" butonu için)
    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
