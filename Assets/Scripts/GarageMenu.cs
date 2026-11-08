using UnityEngine;
using TMPro;
using System.Collections.Generic;

// Ana menüdeki Garaj ekranı. MainMenu sahnesindeki Canvas'a eklenir.
// Ana panel ile garaj paneli arasında geçiş yapar, yükseltme satırlarını oluşturur.
public class GarageMenu : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject mainPanel;
    public GameObject garagePanel;

    [Header("Garaj İçeriği")]
    public TMP_Text moneyText;
    public Transform rowContainer;       // Satırların dizileceği obje (Vertical Layout Group)
    public UpgradeRow rowPrefab;
    public UpgradeDefinition[] upgrades;

    List<UpgradeRow> rows = new List<UpgradeRow>();

    void Start()
    {
        // Her yükseltme için prefab'dan bir satır üret
        if (rowPrefab != null && rowContainer != null)
        {
            foreach (UpgradeDefinition u in upgrades)
            {
                if (u == null) continue;
                UpgradeRow row = Instantiate(rowPrefab, rowContainer);
                row.Setup(u, this);
                rows.Add(row);
            }
        }

        CloseGarage();   // Menü her zaman ana panelle açılsın
    }

    public void OpenGarage()
    {
        mainPanel.SetActive(false);
        garagePanel.SetActive(true);
        RefreshAll();
    }

    public void CloseGarage()
    {
        garagePanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void RefreshAll()
    {
        if (moneyText != null)
            moneyText.text = "Kasa: " + SaveData.Money.ToString("0") + " TL";
        foreach (UpgradeRow row in rows) row.Refresh();
    }
}
