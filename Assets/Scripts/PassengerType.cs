using UnityEngine;

// Bir yolcu tipinin TANIMI (ScriptableObject). Yükseltmelerdeki yöntemin aynısı:
// her tip Project'te bir veri dosyası. Yeni bir tip eklemek için kod yazmaya gerek yok.
// Varsayılan değerler "Normal" yolcuya göre ayarlı.
[CreateAssetMenu(menuName = "TaxiSim/Passenger Type", fileName = "NewPassengerType")]
public class PassengerType : ScriptableObject
{
    [Header("Görünüm")]
    public string displayName = "Normal";
    public Color color = Color.white;                 // Ekranda tip adının rengi
    public string pickupMessage = "Yolcu bindi!";     // Binerken ekranda çıkan söz

    [Header("Ne Sıklıkla Çıkar")]
    [Tooltip("Diğer tiplere göre ağırlık. Örneğin 50 ve 5 ise ilki 10 kat daha sık çıkar.")]
    public float spawnWeight = 50f;

    [Header("Kurallar (çarpanlar: 1 = normal)")]
    public float fareMultiplier = 1f;      // Taksimetre ücreti çarpanı
    public float timeMultiplier = 1f;      // Süre çarpanı (0.8 = %20 daha az süre)
    [Range(0f, 1f)] public float tipPercent = 0.3f;   // Tam memnuniyette bahşiş oranı
    public float penaltyMultiplier = 1f;   // Çarpışma ve kaldırım cezası çarpanı

    [Header("Özel Davranış")]
    [Tooltip("Bu hızın üstünde yolcu rahatsız olur. 0 = hız sınırı yok.")]
    public float speedLimitKmh = 0f;
    public float speedPenaltyPerSecond = 20f;   // Sınır aşıldıkça saniyede memnuniyet kaybı
    [Tooltip("Açıksa bahşiş kalan süreye bağlı: ne kadar erken varırsan o kadar çok (en fazla 2 kat).")]
    public bool tipRewardsSpeed = false;
}
