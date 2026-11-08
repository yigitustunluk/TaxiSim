using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

// Ana menüdeki "Yeni Oyun" ve "Oyun Yükle" ekranı. MainMenu sahnesindeki Canvas'a eklenir.
// İki buton da aynı ekranı açar, sadece "mod" farklıdır.
public class SlotMenu : MonoBehaviour
{
    public string gameSceneName = "SampleScene";

    [Header("Paneller")]
    public GameObject mainPanel;
    public GameObject slotPanel;
    public TMP_Text titleText;

    [Header("Yuvalar")]
    public Transform rowContainer;
    public SlotRow rowPrefab;

    [Header("Onay Penceresi (sonraki adımda)")]
    public GameObject confirmPanel;
    public TMP_Text confirmText;

    List<SlotRow> rows = new List<SlotRow>();
    bool loadMode;
    int pendingSlot = -1;     // Onay bekleyen yuva

    void Start()
    {
        for (int i = 0; i < SaveData.SlotCount; i++)
        {
            SlotRow row = Instantiate(rowPrefab, rowContainer);
            row.Setup(i, this);
            rows.Add(row);
        }
        slotPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ---------- Butonlar ----------

    public void OpenNewGame() { Open(false); }
    public void OpenLoadGame() { Open(true); }

    public void Close()
    {
        slotPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    void Open(bool load)
    {
        loadMode = load;
        titleText.text = load ? "OYUN YÜKLE" : "YENİ OYUN";
        foreach (SlotRow r in rows) r.Refresh(loadMode);
        mainPanel.SetActive(false);
        slotPanel.SetActive(true);
    }

    // ---------- Yuva seçimi ----------

    public void OnSlotClicked(int slot)
    {
        if (loadMode)
        {
            if (SaveData.LoadGame(slot)) StartGame();
            return;
        }

        // Yeni oyun: yuva doluysa önce onay iste
        if (SaveData.SlotExists(slot) && confirmPanel != null)
        {
            pendingSlot = slot;
            confirmText.text = "Kayıt " + (slot + 1) + " silinip yeni oyun başlatılacak.\nEmin misin?";
            confirmPanel.SetActive(true);
            return;
        }

        SaveData.NewGame(slot);
        StartGame();
    }

    public void ConfirmYes()
    {
        confirmPanel.SetActive(false);
        SaveData.NewGame(pendingSlot);
        StartGame();
    }

    public void ConfirmNo()
    {
        confirmPanel.SetActive(false);
        pendingSlot = -1;
    }

    void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }
}
