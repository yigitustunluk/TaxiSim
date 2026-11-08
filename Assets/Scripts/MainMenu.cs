using UnityEngine;
using UnityEngine.SceneManagement;

// Ana menü. MainMenu sahnesindeki Canvas'a eklenir.
// "Oyna" butonu oyun sahnesini yükler, "Çık" butonu oyunu kapatır.
public class MainMenu : MonoBehaviour
{
    // Oyun sahnesinin adı. Build Profiles'taki sahne listesinde olmak zorunda,
    // yoksa Unity onu yükleyemez.
    public string gameSceneName = "SampleScene";

    public void Play()
    {
        // Duraklatılmış bir oyundan dönülmüş olabilir, zamanı garantiye al
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
