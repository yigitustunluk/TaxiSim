using UnityEngine;
using System.Collections.Generic;

// Şehrin yollarında kendi başına dolaşan bir trafik aracı.
//
// Nasıl çalışır?
// - Şehir bir ızgara. Yolların kesiştiği her kavşağın bir "adresi" var: (x, z) gibi tam sayılar.
// - Araç bir kavşaktan komşu kavşağa gider. Her kavşakta rastgele yeni bir yön seçer,
//   ama geldiği yöne geri dönmez (U dönüşü yapmaz).
// - Sağ şeritten gider (Türkiye'deki gibi).
// - Önünde başka bir araç (taksi dahil) varsa durur.
[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public CityGenerator city;

    [Header("Sürüş")]
    public float speed = 10f;            // m/s (≈ 36 km/s)
    public float acceleration = 8f;      // Kalkış ve duruş yumuşaklığı
    public float turnSpeed = 180f;       // Saniyede kaç derece döner
    public float brakeDistance = 8f;     // Önündeki araca bu mesafede durur

    Rigidbody rb;
    float cell;                          // Bir blok + bir yol genişliği
    int minIndex, maxIndex;              // Geçerli kavşak adresleri aralığı

    Vector2Int currentNode;              // Çıktığımız kavşak
    Vector2Int nextNode;                 // Gittiğimiz kavşak
    Vector2Int direction;                // Izgara üzerindeki yön: (1,0), (-1,0), (0,1), (0,-1)

    // Her yol parçasında iki hedef nokta var: parçanın başı ve sonu (sağ şeritte).
    // Kavşağın içindeki dönüş, bir parçanın sonundan diğerinin başına giderken olur.
    Vector3[] waypoints = new Vector3[2];
    int waypointIndex;

    float currentSpeed;
    System.Random rng = new System.Random();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;   // Fizik motoru itmez, onu biz hareket ettiririz. Taksi yine de ona çarpabilir.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        cell = city.blockSize + city.roadWidth;
        int half = city.blocksPerSide / 2;
        minIndex = -half;
        maxIndex = city.blocksPerSide - half;

        // Başlangıç konumuna en yakın kavşağı bul
        currentNode = new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(transform.position.x / cell), minIndex, maxIndex),
            Mathf.Clamp(Mathf.RoundToInt(transform.position.z / cell), minIndex, maxIndex));

        direction = Vector2Int.zero;   // Henüz bir yönümüz yok, ilk seçimde her yön serbest
        ChooseNextDirection();
        PlanSegment();

        // Aracı ilk yol parçasının başına koy ve doğru yöne çevir
        transform.position = waypoints[0];
        transform.rotation = Quaternion.LookRotation(DirectionToWorld(direction));
        waypointIndex = 1;
    }

    void FixedUpdate()
    {
        Vector3 target = waypoints[waypointIndex];
        Vector3 toTarget = target - rb.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        // Gideceğimiz yönde başka bir araç var mı?
        // İnce bir ışın yerine araç genişliğinde bir "kutu" fırlatıyoruz (BoxCast) ve bunu
        // aracın burnunun baktığı yöne değil, GİDECEĞİ yöne atıyoruz. Böylece dönüş yaparken
        // yolunun üstündeki araçları da görür.
        Vector3 moveDirection = distance > 0.01f ? toTarget.normalized : transform.forward;
        bool blocked = IsPathBlocked(moveDirection);

        // Biri bize çarptıysa bir süre bekle (taksiyi sürüklemesin diye)
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            blocked = true;
            currentSpeed = 0f;
        }

        // Hızı yumuşakça hedef hıza yaklaştır (engel varsa 0'a)
        float desiredSpeed = blocked ? 0f : speed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.fixedDeltaTime);

        if (distance > 0.01f)
        {
            // Hedefe doğru dön
            Quaternion look = Quaternion.LookRotation(toTarget);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, look, turnSpeed * Time.fixedDeltaTime));

            // Hedefe doğru ilerle (hedefi geçmeyecek kadar)
            float step = Mathf.Min(currentSpeed * Time.fixedDeltaTime, distance);
            rb.MovePosition(rb.position + toTarget.normalized * step);
        }

        if (distance < 0.5f) AdvanceWaypoint();
    }

    // Önümüzdeki yolu araç genişliğinde bir kutuyla tarar.
    // Kendi çarpışma kutumuzu ve Rigidbody'si olmayan şeyleri (bina, kaldırım) yok sayar.
    bool IsPathBlocked(Vector3 moveDirection)
    {
        Vector3 origin = rb.position + Vector3.up * 0.75f;
        Vector3 halfSize = new Vector3(1.1f, 0.5f, 0.5f);   // Aracın yarı genişliği, yarı yüksekliği
        Quaternion orientation = Quaternion.LookRotation(moveDirection);

        RaycastHit[] hits = Physics.BoxCastAll(origin, halfSize, moveDirection, orientation,
                                               brakeDistance + 2f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.rigidbody == null) continue;   // Araç değil
            if (h.rigidbody == rb) continue;     // Kendimiz
            return true;
        }
        return false;
    }

    // Bir araçla temas ettiğimiz sürece dur. Kinematik araç kimseden etkilenmez,
    // bu yüzden durmazsa taksiyi buldozer gibi iter.
    float waitTimer;
    void OnCollisionStay(Collision collision)
    {
        if (collision.rigidbody != null) waitTimer = 1.5f;
    }

    void AdvanceWaypoint()
    {
        if (waypointIndex == 0)
        {
            waypointIndex = 1;          // Parçanın başına geldik, şimdi sonuna git
        }
        else
        {
            currentNode = nextNode;     // Parçanın sonuna, yani kavşağa geldik
            ChooseNextDirection();      // Yeni yön seç
            PlanSegment();
            waypointIndex = 0;          // Yeni parçanın başına git (kavşakta dönüş)
        }
    }

    // Bulunduğumuz kavşaktan gidilebilecek yönlerden birini rastgele seçer
    void ChooseNextDirection()
    {
        Vector2Int[] allDirections = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        List<Vector2Int> options = new List<Vector2Int>();

        foreach (Vector2Int d in allDirections)
        {
            if (d == -direction) continue;                // Geldiğimiz yöne geri dönme
            Vector2Int n = currentNode + d;
            bool insideCity = n.x >= minIndex && n.x <= maxIndex && n.y >= minIndex && n.y <= maxIndex;
            if (insideCity) options.Add(d);
        }

        if (options.Count == 0) options.Add(-direction);  // Çıkmaz: mecburen geri dön

        direction = options[rng.Next(options.Count)];
        nextNode = currentNode + direction;
    }

    // Seçilen yol parçasının başlangıç ve bitiş noktalarını hesaplar (sağ şeritte)
    void PlanSegment()
    {
        Vector3 forward = DirectionToWorld(direction);
        Vector3 right = Vector3.Cross(Vector3.up, forward);   // İleriye göre sağ taraf
        Vector3 laneOffset = right * (city.roadWidth / 4f);   // Yolun sağ yarısının ortası
        float edge = city.roadWidth / 2f;                     // Kavşağın kenarı

        waypoints[0] = NodeToWorld(currentNode) + forward * edge + laneOffset;
        waypoints[1] = NodeToWorld(nextNode) - forward * edge + laneOffset;
    }

    Vector3 NodeToWorld(Vector2Int node)
    {
        return new Vector3(node.x * cell, 0f, node.y * cell);
    }

    Vector3 DirectionToWorld(Vector2Int d)
    {
        return new Vector3(d.x, 0f, d.y);
    }
}
