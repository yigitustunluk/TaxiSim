using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Duraklatma menüsü. Canvas objesine eklenir.
// ESC (ya da P) ile oyun durur, menü açılır. Tekrar basınca devam eder.
//
// Oyunu nasıl "durduruyoruz"?
// Time.timeScale = 0 yapınca Unity'de zaman akmaz: fizik durur, Time.deltaTime 0 olur.
// Bizim süre, taksimetre, trafik gibi her şeyimiz deltaTime'a bağlı olduğu için hepsi kendiliğinden durur.
public class PauseMenu : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public GameObject pausePanel;     // Açılıp kapanacak panel
    public TMP_Text infoText;         // Paneldeki başlık ve kontroller yazısı

    public bool IsPaused { get; private set; }

    void Start()
    {
        if (infoText != null)
        {
            infoText.text =
                "<size=150%><b>DURAKLATILDI</b></size>\n\n" +
                "W / S  —  Gaz / Geri\n" +
                "A / D  —  Direksiyon\n" +
                "Space  —  Fren\n" +
                "H  —  Korna\n" +
                "T (basılı tut)  —  Zamanı hızlandır\n" +
                "ESC  —  Duraklat / Devam";
        }
        SetPaused(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // wasPressedThisFrame: sadece basıldığı ilk karede true olur (basılı tutunca tekrar etmez)
        if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
            SetPaused(!IsPaused);
    }

    // Butonlardan çağrılacağı için "public"
    public void Resume()
    {
        SetPaused(false);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        // Editörde Application.Quit çalışmaz, onun yerine Play modunu kapatıyoruz
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;           // Motor sesi de dursun
        if (pausePanel != null) pausePanel.SetActive(paused);
    }

    // Sahne kapanırken zamanı mutlaka geri aç. Yoksa bir sonraki sahne donuk başlar.
    void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
