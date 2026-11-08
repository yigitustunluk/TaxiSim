using UnityEngine;
using TMPro;
using System.Collections.Generic;

// Garaj ekranı. Oyun sahnesindeki Canvas'a eklenir, duraklatma menüsünden açılır.
public class GarageMenu : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject mainPanel;         // Garajdan "Geri" ile dönülecek panel (duraklatma paneli)
    public GameObject garagePanel;

    [Header("Garaj İçeriği")]
    public TMP_Text moneyText;
    public Transform rowContainer;       // Satırların dizileceği obje (Vertical Layout Group)
    public UpgradeRow rowPrefab;
    public UpgradeDefinition[] upgrades;

    [Header("Taksi")]
    public TaxiUpgrades taxiUpgrades;    // Satın alınca yükseltmeyi anında uygulamak için

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

        garagePanel.SetActive(false);    // Başta kapalı (duraklatma paneline dokunmuyoruz)
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

    // Satın almadan sonra (ve garaj açılınca) çağrılır
    public void RefreshAll()
    {
        if (moneyText != null)
            moneyText.text = "Kasa: " + SaveData.Money.ToString("0") + " TL";
        foreach (UpgradeRow row in rows) row.Refresh();
        if (taxiUpgrades != null) taxiUpgrades.Apply();
    }
}
