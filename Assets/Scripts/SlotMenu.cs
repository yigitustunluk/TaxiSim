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

    // Onay penceresi iki iş için kullanılıyor: üzerine yazma ve silme.
    // Hangisini beklediğimizi ve hangi yuva için olduğunu hatırlamamız gerekiyor.
    enum PendingAction { None, Overwrite, Delete }
    PendingAction pendingAction = PendingAction.None;
    int pendingSlot = -1;

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
            AskConfirm(PendingAction.Overwrite, slot,
                "Kayıt " + (slot + 1) + " silinip yeni oyun başlatılacak.\nEmin misin?");
            return;
        }

        SaveData.NewGame(slot);
        StartGame();
    }

    // Satırdaki "Sil" butonundan çağrılır
    public void OnDeleteClicked(int slot)
    {
        AskConfirm(PendingAction.Delete, slot,
            "Kayıt " + (slot + 1) + " kalıcı olarak silinecek.\nEmin misin?");
    }

    void AskConfirm(PendingAction action, int slot, string question)
    {
        pendingAction = action;
        pendingSlot = slot;
        confirmText.text = question;
        confirmPanel.SetActive(true);
    }

    public void ConfirmYes()
    {
        confirmPanel.SetActive(false);

        switch (pendingAction)
        {
            case PendingAction.Overwrite:
                SaveData.NewGame(pendingSlot);
                StartGame();
                break;
            case PendingAction.Delete:
                SaveData.DeleteSlot(pendingSlot);
                foreach (SlotRow r in rows) r.Refresh(loadMode);   // Silinen yuva "Boş" görünsün
                break;
        }
        pendingAction = PendingAction.None;
    }

    public void ConfirmNo()
    {
        confirmPanel.SetActive(false);
        pendingAction = PendingAction.None;
        pendingSlot = -1;
    }

    void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }
}
