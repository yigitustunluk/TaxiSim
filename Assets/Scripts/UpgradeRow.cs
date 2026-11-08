using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Garaj ekranındaki tek bir yükseltme satırı: bilgi yazısı + "Satın Al" butonu.
// Bu bir prefab olacak. GarageMenu, her yükseltme için bir kopyasını oluşturur.
public class UpgradeRow : MonoBehaviour
{
    [Header("Bağlantılar (prefab içinde sürükle)")]
    public TMP_Text infoText;
    public Button buyButton;
    public TMP_Text buyButtonText;

    UpgradeDefinition upgrade;
    GarageMenu garage;

    // GarageMenu, satırı oluşturduktan hemen sonra bunu çağırır
    public void Setup(UpgradeDefinition def, GarageMenu owner)
    {
        upgrade = def;
        garage = owner;

        // Butonu KODLA bağlıyoruz. Inspector'daki On Click'in kod hâli.
        // Prefab'dan üretilen satırlarda Inspector'dan bağlamak mümkün değil, çünkü
        // hangi yükseltmeye ait olduğu oyun başlamadan belli değil.
        buyButton.onClick.AddListener(OnBuyClicked);
        Refresh();
    }

    void OnBuyClicked()
    {
        if (upgrade.TryBuy()) garage.RefreshAll();   // Para değişti, bütün satırlar güncellensin
    }

    public void Refresh()
    {
        // Seviye göstergesi: "Seviye 1 / 3"
        // (■ gibi semboller kullanmıyoruz: varsayılan yazı tipinde olmayabilirler)
        string level = "Seviye " + upgrade.CurrentLevel + " / " + upgrade.MaxLevel;

        infoText.text = "<b>" + upgrade.displayName + "</b>   <size=80%>" + level + "</size>\n" +
                        "<size=75%>" + upgrade.description + "</size>";

        if (upgrade.IsMaxed)
        {
            buyButtonText.text = "TAMAM";
            buyButton.interactable = false;
        }
        else
        {
            buyButtonText.text = upgrade.NextCost + " TL";
            buyButton.interactable = upgrade.CanBuy;   // Para yetmiyorsa buton soluk görünür
        }
    }
}
