using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Kayıt ekranındaki tek bir yuva satırı. Satırın kendisi büyük bir buton.
// Prefab olacak, SlotMenu her yuva için bir kopyasını üretir.
public class SlotRow : MonoBehaviour
{
    [Header("Bağlantılar (prefab içinde sürükle)")]
    public TMP_Text infoText;
    public Button button;

    int slot;
    SlotMenu menu;

    public void Setup(int slotIndex, SlotMenu owner)
    {
        slot = slotIndex;
        menu = owner;
        button.onClick.AddListener(() => menu.OnSlotClicked(slot));
        // "() => ..." bir "lambda": tıklanınca çalışacak küçük, isimsiz bir fonksiyon.
        // Her satır kendi yuva numarasını hatırlar.
    }

    // loadMode: "Oyun Yükle" ekranındaysak boş yuvalar tıklanamaz
    public void Refresh(bool loadMode)
    {
        SaveGame data = SaveData.PeekSlot(slot);
        string title = "<b>Kayıt " + (slot + 1) + "</b>";

        if (data == null)
        {
            infoText.text = title + "\n<size=75%>Boş</size>";
            button.interactable = !loadMode;
        }
        else
        {
            infoText.text = title + "\n<size=75%>" +
                            data.money.ToString("0") + " TL  ·  " +
                            data.completedRides + " yolculuk  ·  " +
                            data.lastPlayed + "</size>";
            button.interactable = true;
        }
    }
}
