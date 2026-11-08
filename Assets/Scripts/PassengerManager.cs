using UnityEngine;

// Oyunun ana döngüsü:
// 1) Yolda yeşil bir işaret çıkar: yolcu orada bekliyor.
// 2) İşaretin içinde durursan yolcu biner, turuncu bir varış işareti çıkar ve süre başlar.
// 3) Varış işaretinde durursan yolcu iner ve ücreti kasana eklenir.
//    Zamanında gelirsen bahşiş alırsın, geç kalırsan ücret kesilir.
//    Yolcu taşırken yaptığın çarpışmalar yolcu memnuniyetini, o da bahşişi düşürür.
public class PassengerManager : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public TaxiController taxi;
    public CityGenerator city;

    [Header("Kurallar")]
    public float stopRadius = 5f;          // İşaretin yarıçapı (metre)
    public float maxStopSpeedKmh = 5f;     // Bu hızın altındaysan "durmuş" sayılırsın
    public float baseFare = 15f;           // Açılış ücreti (TL)
    public float farePerMeter = 0.2f;      // Metre başına ücret (TL)
    public float farePerWaitSecond = 0.5f; // Dururken (trafikte beklerken) saniye başına ücret (TL)

    [Header("Yolcu Tipleri")]
    public PassengerType[] passengerTypes;   // Boş bırakılırsa herkes normal yolcu olur

    [Header("Süre ve Bahşiş")]
    public float expectedSpeed = 12f;      // Süre hesabı için beklenen ortalama hız (m/s)
    public float extraTime = 5f;           // Süreye eklenen ek pay (saniye)
    public float tipPercent = 0.3f;        // Tam memnuniyette bahşiş: ücretin %30'u
    public float latePenalty = 0.3f;       // Geç kalınca ücretten kesilen oran: %30
    public float crashPenalty = 15f;       // Her çarpışmada memnuniyet kaybı (puan)
    public float sidewalkPenalty = 25f;    // Kaldırımda geçen her saniye memnuniyet kaybı (puan)

    // Oyuncu şu an ne yapıyor? Bu tür "durum" listelerine enum denir.
    enum State { GoingToPickup, CarryingPassenger }
    State state;

    GameObject marker;          // Ekrandaki işaret (zemin halkası + ışık sütunu)
    Vector3 targetPos;          // Şu anki hedefin konumu
    float currentFare;          // Taksimetrede yazan ücret (yolculuk boyunca artar)
    float estimatedFare;        // En kısa yoldan gidilseydi tutacak ücret (tahmini)
    float meterDistance;        // Yolcu bindikten sonra gidilen gerçek mesafe (metre)
    float waitTime;             // Yolcu taksideyken durarak geçen süre (saniye)
    Vector3 lastTaxiPos;        // Taksinin bir önceki karedeki konumu
    float totalTime;            // Yolculuk başındaki toplam süre (aceleci yolcunun bahşişi için)
    float timeLeft;             // Kalan süre (saniye). Eksiye düşerse geç kalmışsın.
    float satisfaction;         // Yolcu memnuniyeti (0-100)
    PassengerType currentType;  // Bekleyen ya da taşınan yolcunun tipi
    float totalMoney;           // Kasadaki toplam para (kayıttan yüklenir, her yolculukta kaydedilir)
    int completedRides;         // Tamamlanan yolculuk sayısı
    string message = "";        // Ekranda kısa süre görünen mesaj
    float messageTimer;

    System.Random rng = new System.Random(); // Şehirden bağımsız rastgele sayı üretici

    // Diğer script'lerin (örneğin yön oku) okuyabilmesi için dışarıya açılan bilgiler.
    // Sadece okunabilirler, dışarıdan değiştirilemezler.
    public Vector3 TargetPosition => targetPos;
    public bool IsCarryingPassenger => state == State.CarryingPassenger;
    public float CurrentFare => currentFare;
    public float MeterDistance => meterDistance;
    public float TimeLeft => timeLeft;
    public float Satisfaction => satisfaction;
    public PassengerType CurrentType => currentType;
    public float SpeedLimitKmh => currentType != null ? currentType.speedLimitKmh : 0f;
    public bool IsOverSpeedLimit => IsCarryingPassenger && SpeedLimitKmh > 0f && taxi.SpeedKmh > SpeedLimitKmh;
    public float TotalMoney => SaveData.Money;   // Garajda harcanınca da güncel kalsın
    public int CompletedRides => completedRides;
    public string Message => message;
    public bool HasMessage => messageTimer > 0f;

    void Start()
    {
        taxi.OnCrash += HandleCrash;   // Taksinin çarpışma duyurusuna abone ol
        totalMoney = SaveData.Money;              // Kayıttan yükle
        completedRides = SaveData.CompletedRides;
        NewPickup();
    }

    void OnDestroy()
    {
        if (taxi != null) taxi.OnCrash -= HandleCrash;  // Aboneliği bırak (iyi alışkanlık)
    }

    void Update()
    {
        if (taxi == null) return;
        if (messageTimer > 0f) messageTimer -= Time.deltaTime;
        if (state == State.CarryingPassenger)
        {
            timeLeft -= Time.deltaTime;
            RunMeter();

            // Hız sınırı olan yolcu (hassas, VIP) hızlı sürünce rahatsız olur
            if (IsOverSpeedLimit)
            {
                satisfaction = Mathf.Max(0f, satisfaction - currentType.speedPenaltyPerSecond * Time.deltaTime);
                ShowMessage("Çok hızlısın! Yolcu rahatsız");
            }

            // Kaldırımda sürmek yolcuyu korkutur
            if (taxi.IsOnSidewalk)
            {
                satisfaction = Mathf.Max(0f, satisfaction - sidewalkPenalty * PenaltyMult * Time.deltaTime);
                ShowMessage("Kaldırımdan in! Yolcu korkuyor");
            }
        }

        // İşaretin içinde ve neredeyse durmuş muyuz?
        if (DistanceToTarget() < stopRadius && taxi.SpeedKmh < maxStopSpeedKmh)
        {
            if (state == State.GoingToPickup) PickUp();
            else DropOff();
        }
    }

    // Yeni bir yolcu oluşturur (taksiden en az 60 m uzakta)
    void NewPickup()
    {
        state = State.GoingToPickup;
        currentType = ChooseType();
        targetPos = RandomRoadPoint(taxi.transform.position, 60f);
        PlaceMarker(targetPos, Color.green);
    }

    void PickUp()
    {
        Vector3 pickupPos = targetPos;
        state = State.CarryingPassenger;
        targetPos = RandomRoadPoint(pickupPos, 80f);

        // Taksimetreyi sıfırla: açılış ücretiyle başlar
        meterDistance = 0f;
        waitTime = 0f;
        lastTaxiPos = FlatTaxiPosition();
        currentFare = baseFare * FareMult;

        // Tahmini ücret: şehir ızgara olduğu için en kısa yol "Manhattan mesafesi"dir
        // (yatay fark + dikey fark). Kuş uçuşu ile gidilemez, binalar var.
        float manhattan = Mathf.Abs(targetPos.x - pickupPos.x) + Mathf.Abs(targetPos.z - pickupPos.z);
        estimatedFare = (baseFare + manhattan * farePerMeter) * FareMult;

        // Süre hesabı için kuş uçuşu mesafe (bu değerleri sen ayarladın, bozmuyoruz)
        float distance = Vector3.Distance(pickupPos, targetPos);

        // Süre: mesafeyi beklenen hızla gitmek için gereken zaman + ek pay
        // Hız sınırı olan yolcuda beklenen hızı sınıra göre düşür. Yoksa sınıra uyan oyuncu
        // hiçbir zaman yetişemez. Virajlar ve trafik için sınırın %60'ı ortalama kabul edilir.
        float speed = expectedSpeed;
        if (SpeedLimitKmh > 0f) speed = Mathf.Min(speed, SpeedLimitKmh / 3.6f * 0.6f);

        timeLeft = (distance / speed + extraTime) * TimeMult;
        totalTime = timeLeft;
        satisfaction = 100f;

        PlaceMarker(targetPos, new Color(1f, 0.55f, 0f)); // Turuncu
        string greeting = currentType != null ? currentType.pickupMessage : "Yolcu bindi!";
        ShowMessage(greeting + " " + timeLeft.ToString("0") + " saniyen var.");
    }

    void DropOff()
    {
        float payment;
        float tip = 0f;

        if (timeLeft >= 0f)
        {
            payment = currentFare;
            tip = currentFare * TipPct * (satisfaction / 100f);

            // Aceleci yolcu: erken varırsan bahşiş artar (süre bitmek üzereyken yarıya, hiç süre harcamadan 2 katına kadar)
            if (currentType != null && currentType.tipRewardsSpeed && totalTime > 0f)
                tip *= Mathf.Lerp(0.5f, 2f, timeLeft / totalTime);
            ShowMessage("Zamanında! " + payment.ToString("0") + " TL + " + tip.ToString("0") + " TL bahşiş");
        }
        else
        {
            // Geç kalınca yolcu taksimetreye itiraz eder: en fazla tahmini ücreti öder,
            // o da %30 eksik. Böylece yolu bilerek uzatıp taksimetreyi şişirmek işe yaramaz.
            payment = Mathf.Min(currentFare, estimatedFare) * (1f - latePenalty);
            ShowMessage("Geç kaldın! Sadece " + payment.ToString("0") + " TL ödendi");
        }

        SaveData.RecordRide(payment + tip);       // Otomatik kayıt
        totalMoney = SaveData.Money;
        completedRides = SaveData.CompletedRides;
        NewPickup();
    }

    // Yolcu tipinin çarpanları. Tip yoksa normal değerler kullanılır.
    float FareMult => currentType != null ? currentType.fareMultiplier : 1f;
    float TimeMult => currentType != null ? currentType.timeMultiplier : 1f;
    float TipPct => currentType != null ? currentType.tipPercent : tipPercent;
    float PenaltyMult => currentType != null ? currentType.penaltyMultiplier : 1f;

    // Ağırlıklı rastgele seçim: ağırlığı büyük olan tip daha sık çıkar.
    // Bütün ağırlıkları bir çizgiye art arda dizip çizgi üzerinde rastgele bir nokta seçmek gibi.
    PassengerType ChooseType()
    {
        if (passengerTypes == null || passengerTypes.Length == 0) return null;

        float total = 0f;
        foreach (PassengerType t in passengerTypes)
            if (t != null) total += t.spawnWeight;

        float pick = (float)rng.NextDouble() * total;
        foreach (PassengerType t in passengerTypes)
        {
            if (t == null) continue;
            pick -= t.spawnWeight;
            if (pick <= 0f) return t;
        }
        return passengerTypes[passengerTypes.Length - 1];
    }

    // TAKSİMETRE: Her karede taksinin ne kadar yol aldığını ölçüp ücrete ekler.
    // Dururken de (trafik, kırmızı ışık) zaman ücreti işler, gerçek taksimetre gibi.
    void RunMeter()
    {
        Vector3 pos = FlatTaxiPosition();
        meterDistance += Vector3.Distance(pos, lastTaxiPos);
        lastTaxiPos = pos;

        if (taxi.SpeedKmh < maxStopSpeedKmh) waitTime += Time.deltaTime;

        currentFare = (baseFare + meterDistance * farePerMeter + waitTime * farePerWaitSecond) * FareMult;
    }

    Vector3 FlatTaxiPosition()
    {
        Vector3 p = taxi.transform.position;
        p.y = 0f;
        return p;
    }

    // Taksi bir şeye çarptığında TaxiController tarafından çağrılır
    void HandleCrash(float impact)
    {
        if (state != State.CarryingPassenger) return;   // Boşken çarpmanın cezası yok
        float loss = crashPenalty * PenaltyMult;
        satisfaction = Mathf.Max(0f, satisfaction - loss);
        ShowMessage("Çarpışma! Memnuniyet -" + loss.ToString("0"));
    }

    // Taksi ile hedef arasındaki yatay mesafe (yüksekliği yok sayar)
    public float DistanceToTarget()
    {
        Vector3 a = taxi.transform.position; a.y = 0f;
        Vector3 b = targetPos; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Şehirdeki yollardan rastgele bir nokta seçer (kaldırım kenarına yakın)
    Vector3 RandomRoadPoint(Vector3 from, float minDistance)
    {
        float cell = city.blockSize + city.roadWidth;
        int half = city.blocksPerSide / 2;
        Vector3 p = Vector3.zero;

        for (int i = 0; i < 50; i++)
        {
            int roadIndex = rng.Next(-half, city.blocksPerSide - half + 1);  // Hangi yol çizgisi
            int blockIndex = rng.Next(-half, city.blocksPerSide - half);     // Yolun hangi bölümü
            float roadCoord = roadIndex * cell;
            float alongCoord = blockIndex * cell + cell / 2f;                // Kavşak değil, blok ortası
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;                  // Yolun sağı mı solu mu
            float curbOffset = side * (city.roadWidth / 2f - 2f);

            if (rng.NextDouble() < 0.5)
                p = new Vector3(roadCoord + curbOffset, 0f, alongCoord);     // Kuzey-güney yolu
            else
                p = new Vector3(alongCoord, 0f, roadCoord + curbOffset);     // Doğu-batı yolu

            Vector3 flatFrom = new Vector3(from.x, 0f, from.z);
            if (Vector3.Distance(flatFrom, p) >= minDistance) return p;
        }
        return p;
    }

    // Hedef işaretini oluşturur: yerde bir halka ve binaların üstünden görünen bir sütun
    void PlaceMarker(Vector3 pos, Color color)
    {
        if (marker != null) Destroy(marker);
        marker = new GameObject("Hedef");

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.transform.SetParent(marker.transform);
        disc.transform.position = pos + Vector3.up * 0.05f;
        disc.transform.localScale = new Vector3(stopRadius * 2f, 0.05f, stopRadius * 2f);

        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.transform.SetParent(marker.transform);
        beam.transform.position = pos + Vector3.up * 20f;
        beam.transform.localScale = new Vector3(0.8f, 20f, 0.8f);  // 40 m yüksekliğinde

        // İşaretin içinden geçebilmek için çarpışmayı kapat
        foreach (Collider col in marker.GetComponentsInChildren<Collider>())
            col.enabled = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.color = color;
        disc.GetComponent<Renderer>().sharedMaterial = m;
        beam.GetComponent<Renderer>().sharedMaterial = m;
    }

    void ShowMessage(string text)
    {
        message = text;
        messageTimer = 3f;
    }
}
