using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

// Bir kayıt yuvasında saklanan veriler.
// [Serializable]: Unity'nin JsonUtility'si bu sınıfı JSON metnine çevirebilsin diye.
[Serializable]
public class SaveGame
{
    public float money;
    public int completedRides;
    public string lastPlayed;                                  // "08.11.2026 13:45" gibi
    public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
}

// JsonUtility "Dictionary" saklayamıyor, o yüzden yükseltmeleri liste olarak tutuyoruz
[Serializable]
public class UpgradeLevel
{
    public string id;
    public int level;
}

// Kayıt sistemi: 3 yuva, her yuva ayrı bir JSON dosyası.
//
// Dosyalar Application.persistentDataPath klasörüne yazılır. Windows'ta:
// C:\Users\<kullanıcı>\AppData\LocalLow\<şirket adı>\TaxiSim\save_0.json
//
// "static" sınıf: Sahnede durmaz, sahne değişse de seçili yuva ve veriler kaybolmaz.
public static class SaveData
{
    public const int SlotCount = 3;

    public static int CurrentSlot { get; private set; } = -1;
    static SaveGame current;

    // ---------- Yuva işlemleri (menü kullanır) ----------

    static string SlotPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, "save_" + slot + ".json");
    }

    public static bool SlotExists(int slot)
    {
        return File.Exists(SlotPath(slot));
    }

    // Menüde yuva bilgisini göstermek için okur. Yuva boşsa ya da dosya bozuksa null döner.
    public static SaveGame PeekSlot(int slot)
    {
        if (!SlotExists(slot)) return null;
        try
        {
            return JsonUtility.FromJson<SaveGame>(File.ReadAllText(SlotPath(slot)));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Kayıt " + slot + " okunamadı: " + e.Message);
            return null;
        }
    }

    // Seçilen yuvada sıfırdan yeni oyun başlatır (varsa eski kaydın üzerine yazar)
    public static void NewGame(int slot)
    {
        CurrentSlot = slot;
        current = new SaveGame();
        Save();
    }

    // Seçilen yuvadaki kaydı yükler. Başarılıysa true döner.
    public static bool LoadGame(int slot)
    {
        SaveGame data = PeekSlot(slot);
        if (data == null) return false;
        CurrentSlot = slot;
        current = data;
        return true;
    }

    public static void DeleteSlot(int slot)
    {
        if (SlotExists(slot)) File.Delete(SlotPath(slot));
        if (CurrentSlot == slot) { CurrentSlot = -1; current = null; }
    }

    public static void Save()
    {
        if (current == null || CurrentSlot < 0) return;
        current.lastPlayed = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        try
        {
            // true = okunabilir, girintili JSON
            File.WriteAllText(SlotPath(CurrentSlot), JsonUtility.ToJson(current, true));
        }
        catch (Exception e)
        {
            Debug.LogError("Kayıt yazılamadı: " + e.Message);
        }
    }

    // Oyun sahnesi menüden geçilmeden doğrudan açıldıysa (editörde test ederken)
    // henüz seçili bir yuva yoktur. O durumda 1. yuvayı kullan.
    static SaveGame Data
    {
        get
        {
            if (current == null)
            {
                if (!LoadGame(0)) NewGame(0);
                Debug.Log("Yuva seçilmemişti, Kayıt 1 kullanılıyor: " + SlotPath(0));
            }
            return current;
        }
    }

    // ---------- Oyun verileri (oyun kullanır) ----------

    public static float Money
    {
        get => Data.money;
        set { Data.money = value; Save(); }
    }

    public static int CompletedRides => Data.completedRides;

    // Tamamlanan bir yolculuğu tek seferde kaydeder (para + yolculuk sayısı)
    public static void RecordRide(float earned)
    {
        Data.money += earned;
        Data.completedRides++;
        Save();
    }

    public static int GetLevel(string upgradeId)
    {
        UpgradeLevel u = Data.upgrades.Find(x => x.id == upgradeId);
        return u != null ? u.level : 0;
    }

    public static void SetLevel(string upgradeId, int level)
    {
        UpgradeLevel u = Data.upgrades.Find(x => x.id == upgradeId);
        if (u == null)
        {
            u = new UpgradeLevel { id = upgradeId };
            Data.upgrades.Add(u);
        }
        u.level = level;
        Save();
    }
}
