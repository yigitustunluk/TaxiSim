using UnityEngine;
using System.Collections.Generic;

// Şehrin yollarında kendi başına dolaşan bir trafik aracı.
//
// Nasıl çalışır?
// - Şehir bir yol ağı: kavşaklar (düğüm) ve aralarındaki yollar (kenar).
// - Araç bir kavşaktan, bir yol boyunca komşu kavşağa gider. Her kavşakta o kavşaktan
//   çıkan yollardan birini rastgele seçer, ama geldiği yola geri dönmez (U dönüşü yapmaz).
// - Sağ şeritten gider (Türkiye'deki gibi). Şeridin yeri yolun genişliğine göre hesaplanır.
// - Önünde başka bir araç (taksi dahil) varsa durur. Kırmızı ışıkta durur.
[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public CityLayout layout;            // Yol ağı
    public TrafficLightSystem lights;    // Boşsa ya da kapalıysa ışıklara uymaz

    [Header("Sürüş")]
    public float speed = 10f;            // m/s (≈ 36 km/s)
    public float acceleration = 8f;      // Kalkış ve duruş yumuşaklığı
    public float turnSpeed = 180f;       // Saniyede kaç derece döner
    public float brakeDistance = 8f;     // Önündeki araca bu mesafede durur
    // Kırmızıda aracın MERKEZİ kavşak kenarından ne kadar önce durur.
    // Araç 4 m uzun, merkezi ortasında: ön tamponu merkezden 2 m önde.
    // Işık direği kavşaktan 2.5 m önde. 2.5 + 2 + 0.5 boşluk = 5 m: tampon direğin hemen gerisinde durur.
    public float stopMargin = 5f;

    Rigidbody rb;

    RoadNode currentNode;                // Çıktığımız kavşak
    RoadNode nextNode;                   // Gittiğimiz kavşak
    RoadEdge currentEdge;                // Üzerinde gittiğimiz yol
    Vector3 direction;                   // Yolun yönü (birim vektör)

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

        // Başlangıç konumuna en yakın kavşaktan başla
        currentNode = layout.Network.NearestNode(transform.position);
        currentEdge = null;              // Henüz bir yoldan gelmedik, ilk seçimde her yol serbest
        ChooseNextEdge();
        PlanSegment();

        // Aracı ilk yol parçasının başına koy ve doğru yöne çevir
        transform.position = waypoints[0];
        transform.rotation = Quaternion.LookRotation(direction);
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

        // TRAFİK IŞIĞI: Kavşağa yaklaşıyorsak (yol parçasının sonuna gidiyorsak) ışığa bak.
        // waypoints[1], kavşağın tam kenarında, yani "dur çizgisinde".
        bool mustStop = false;
        if (lights != null && lights.isActiveAndEnabled && waypointIndex == 1)
        {
            SignalColor signal = lights.GetSignal(nextNode, direction);
            // Sarıda: çizgiye çok yakınsak geç (aniden duramayız), uzaksak dur
            mustStop = signal == SignalColor.Red || (signal == SignalColor.Yellow && distance > stopMargin + 4f);

            if (mustStop)
            {
                // Çizgiye yaklaştıkça hedef hızı düşür: 13 metreden itibaren yavaşla, çizgide dur
                float slowDown = Mathf.Clamp01((distance - stopMargin) / 12f);
                desiredSpeed = Mathf.Min(desiredSpeed, speed * slowDown);
            }
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.fixedDeltaTime);

        if (distance > 0.01f)
        {
            // Hedefe doğru dön
            Quaternion look = Quaternion.LookRotation(toTarget);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, look, turnSpeed * Time.fixedDeltaTime));

            // Hedefe doğru ilerle (hedefi geçmeyecek kadar).
            // Kırmızıda dur çizgisini asla geçme.
            float maxStep = mustStop ? Mathf.Max(0f, distance - stopMargin) : distance;
            float step = Mathf.Min(currentSpeed * Time.fixedDeltaTime, maxStep);
            rb.MovePosition(rb.position + toTarget.normalized * step);
        }

        if (distance < 0.5f && !mustStop) AdvanceWaypoint();
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
            ChooseNextEdge();           // Yeni yol seç
            PlanSegment();
            waypointIndex = 0;          // Yeni parçanın başına git (kavşakta dönüş)
        }
    }

    // Bulunduğumuz kavşaktan çıkan yollardan birini rastgele seçer
    void ChooseNextEdge()
    {
        List<RoadEdge> options = new List<RoadEdge>();
        foreach (RoadEdge e in currentNode.edges)
            if (e != currentEdge) options.Add(e);        // Geldiğimiz yola geri dönme

        if (options.Count == 0) options.Add(currentEdge); // Çıkmaz: mecburen geri dön

        currentEdge = options[rng.Next(options.Count)];
        nextNode = currentEdge.Other(currentNode);
        direction = currentEdge.DirectionFrom(currentNode);
    }

    // Seçilen yol parçasının başlangıç ve bitiş noktalarını hesaplar (sağ şeritte).
    // Noktalar kavşağın dışında: kavşağın bu yöndeki yarı genişliği kadar içeride.
    void PlanSegment()
    {
        Vector3 right = Vector3.Cross(Vector3.up, direction);       // Gidiş yönüne göre sağ
        Vector3 laneOffset = right * (currentEdge.width / 4f);      // Sağ yarının ortası

        waypoints[0] = currentNode.position + direction * IntersectionHalfSize(currentNode) + laneOffset;
        waypoints[1] = nextNode.position - direction * IntersectionHalfSize(nextNode) + laneOffset;
    }

    // Kavşağın, gidiş yönümüz boyunca yarı genişliği = kesişen yolun yarı genişliği
    float IntersectionHalfSize(RoadNode node)
    {
        bool alongX = Mathf.Abs(direction.x) > 0.5f;
        return (alongX ? layout.xWidths[node.gridIndex.x] : layout.zWidths[node.gridIndex.y]) / 2f;
    }
}
