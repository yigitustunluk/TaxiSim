using UnityEngine;

// Oyunun ana döngüsü:
// 1) Yolda yeşil bir işaret çıkar: yolcu orada bekliyor.
// 2) İşaretin içinde durursan yolcu biner, turuncu bir varış işareti çıkar.
// 3) Varış işaretinde durursan yolcu iner ve ücreti kasana eklenir.
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

    // Oyuncu şu an ne yapıyor? Bu tür "durum" listelerine enum denir.
    enum State { GoingToPickup, CarryingPassenger }
    State state;

    GameObject marker;          // Ekrandaki işaret (zemin halkası + ışık sütunu)
    Vector3 targetPos;          // Şu anki hedefin konumu
    float currentFare;          // Taşınan yolcunun ödeyeceği ücret
    float totalMoney;           // Kasadaki toplam para
    int completedRides;         // Tamamlanan yolculuk sayısı
    string message = "";        // Ekranda kısa süre görünen mesaj
    float messageTimer;

    System.Random rng = new System.Random(); // Şehirden bağımsız rastgele sayı üretici

    void Start()
    {
        NewPickup();
    }

    void Update()
    {
        if (taxi == null) return;
        if (messageTimer > 0f) messageTimer -= Time.deltaTime;

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
        targetPos = RandomRoadPoint(taxi.transform.position, 60f);
        PlaceMarker(targetPos, Color.green);
    }

    void PickUp()
    {
        Vector3 pickupPos = targetPos;
        state = State.CarryingPassenger;
        targetPos = RandomRoadPoint(pickupPos, 80f);

        // Ücret: açılış + kuş uçuşu mesafe x metre ücreti
        float distance = Vector3.Distance(pickupPos, targetPos);
        currentFare = baseFare + distance * farePerMeter;

        PlaceMarker(targetPos, new Color(1f, 0.55f, 0f)); // Turuncu
        ShowMessage("Yolcu bindi! Turuncu işarete götür.");
    }

    void DropOff()
    {
        totalMoney += currentFare;
        completedRides++;
        ShowMessage("Yolcu indi: +" + currentFare.ToString("0") + " TL");
        NewPickup();
    }

    // Taksi ile hedef arasındaki yatay mesafe (yüksekliği yok sayar)
    float DistanceToTarget()
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

    // Ekrandaki görev bilgisi
    void OnGUI()
    {
        if (taxi == null) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22;
        style.normal.textColor = Color.white;

        string task = state == State.GoingToPickup
            ? "Görev: Yeşil işaretteki yolcuyu al"
            : "Görev: Yolcuyu turuncu işarete götür";

        GUI.Box(new Rect(10, 170, 460, 150), "");
        GUI.Label(new Rect(20, 175, 440, 30), task, style);
        GUI.Label(new Rect(20, 210, 440, 30), "Hedefe uzaklık: " + DistanceToTarget().ToString("0") + " m", style);
        if (state == State.CarryingPassenger)
            GUI.Label(new Rect(20, 245, 440, 30), "Yolculuk ücreti: " + currentFare.ToString("0") + " TL", style);
        GUI.Label(new Rect(20, 280, 440, 30), "Kasa: " + totalMoney.ToString("0") + " TL  (" + completedRides + " yolculuk)", style);

        if (messageTimer > 0f)
        {
            GUIStyle big = new GUIStyle(style);
            big.fontSize = 36;
            big.alignment = TextAnchor.MiddleCenter;
            big.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), message, big);
        }
    }
}
