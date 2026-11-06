using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

// Gece-gündüz döngüsü. Directional Light (güneş) objesine eklenir.
// - Güneşi gökyüzünde döndürür, parlaklığını ve rengini saate göre ayarlar.
// - Ortam ışığını (gölgelerin ne kadar karanlık olacağını) gündüz açık, gece koyu yapar.
// - Hava kararınca taksinin farlarını yakar.
// Test için T tuşuna basılı tutarsan zaman 20 kat hızlı akar.
[RequireComponent(typeof(Light))]
public class DayNightCycle : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public Transform taxi;
    public CityGenerator city;      // Sokak lambalarını açıp kapatmak için

    [Header("Zaman")]
    public float dayLengthSeconds = 240f;           // Bir tam gün kaç saniye sürer (4 dakika)
    [Range(0f, 1f)] public float timeOfDay = 0.30f; // 0 = gece yarısı, 0.25 = sabah, 0.5 = öğle, 0.75 = akşam

    [Header("Işık")]
    public float maxSunIntensity = 1.2f;
    public Color dayColor = new Color(1f, 0.96f, 0.88f);       // Öğle: hafif sıcak beyaz
    public Color sunsetColor = new Color(1f, 0.5f, 0.25f);     // Gün doğumu/batımı: turuncu
    public Color dayAmbient = new Color(0.55f, 0.58f, 0.65f);
    public Color nightAmbient = new Color(0.12f, 0.13f, 0.22f); // Gece: ay ışığı gibi soluk lacivert

    [Header("Gökyüzü ve Sis")]
    public Color daySkyFog = new Color(0.62f, 0.70f, 0.80f);   // Gündüz: açık mavi pus
    public Color nightSky = new Color(0.03f, 0.05f, 0.11f);    // Gece: simsiyah değil, gece mavisi
    public float fogDensity = 0.006f;                          // Uzaktaki binalar sise karışır

    Light sun;
    Light[] headlights;
    Camera cam;
    bool lampsOn;

    // Başka script'ler (örneğin saat göstergesi) okuyabilsin: "14:30" gibi
    public string ClockText
    {
        get
        {
            int totalMinutes = Mathf.FloorToInt(timeOfDay * 24f * 60f);
            return (totalMinutes / 60).ToString("00") + ":" + (totalMinutes % 60).ToString("00");
        }
    }

    void Start()
    {
        sun = GetComponent<Light>();
        RenderSettings.ambientMode = AmbientMode.Flat;   // Ortam ışığını tek renkle, kodla yöneteceğiz
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity;
        cam = Camera.main;
        if (taxi != null) headlights = CreateHeadlights();
    }

    void Update()
    {
        // Zamanı ilerlet (T basılıyken 20 kat hızlı)
        float speedUp = (Keyboard.current != null && Keyboard.current.tKey.isPressed) ? 20f : 1f;
        timeOfDay += Time.deltaTime * speedUp / dayLengthSeconds;
        if (timeOfDay >= 1f) timeOfDay -= 1f;

        // Güneşin açısı: 0.25'te doğar (0°), 0.5'te tepede (90°), 0.75'te batar (180°)
        float sunAngle = timeOfDay * 360f - 90f;
        transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

        // Güneşin yüksekliği: -1 (tam altta) ile +1 (tam tepede) arası
        float sunHeight = Mathf.Sin(sunAngle * Mathf.Deg2Rad);
        float daylight = Mathf.Clamp01(sunHeight * 3f);   // Ufkun biraz üstünde tam parlaklığa ulaşır

        sun.intensity = maxSunIntensity * daylight;
        sun.color = Color.Lerp(sunsetColor, dayColor, Mathf.Clamp01(sunHeight * 2f));
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, daylight);

        // Sis rengi: gündüz pus, gece karanlık
        Color fogColor = Color.Lerp(nightSky, daySkyFog, daylight);
        RenderSettings.fogColor = fogColor;

        // Gece gökyüzü: hazır gökyüzü gece simsiyah oluyor. Güneş iyice batınca
        // onun yerine sisle aynı renkte düz bir arka plan kullanıyoruz. Böylece
        // uzaktaki binalar gökyüzüyle yumuşakça birleşir.
        if (cam != null)
        {
            bool deepNight = sunHeight < -0.1f;
            cam.clearFlags = deepNight ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = fogColor;
        }

        // Hava kararmaya başlayınca farları ve sokak lambalarını yak
        bool lightsOn = sunHeight < 0.15f;
        if (headlights != null)
            foreach (Light h in headlights) h.enabled = lightsOn;
        if (lightsOn != lampsOn) SetStreetLamps(lightsOn);   // Sadece değiştiği anda çalışır
    }

    void SetStreetLamps(bool on)
    {
        if (city == null || city.lampHeadMaterial == null) return;
        lampsOn = on;
        foreach (Light l in city.streetLights) l.enabled = on;

        // Lamba başlığını parlat ya da söndür. Parlaklığı 1'in üstüne çıkarınca
        // "Bloom" efekti etrafında hale oluşturur.
        Color glow = on ? city.lampColor * 4f : Color.black;
        city.lampHeadMaterial.SetColor("_EmissionColor", glow);

        // Binaların pencerelerini yak ya da söndür
        Color windows = on ? Color.white * 1.3f : Color.black;
        foreach (Material m in city.buildingMaterials) m.SetColor("_EmissionColor", windows);
    }

    // Taksinin önüne iki spot ışık (far) yerleştirir
    Light[] CreateHeadlights()
    {
        Light[] lights = new Light[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject go = new GameObject(i == 0 ? "Far_Sol" : "Far_Sag");
            go.transform.SetParent(taxi);
            go.transform.localPosition = new Vector3(i == 0 ? -0.6f : 0.6f, 0.2f, 2.1f);
            go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);   // Hafif yere doğru

            Light l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 35f;
            l.spotAngle = 55f;
            l.intensity = 25f;
            l.color = new Color(1f, 0.95f, 0.8f);
            l.shadows = LightShadows.None;   // Performans için gölgesiz
            l.enabled = false;
            lights[i] = l;
        }
        return lights;
    }
}
