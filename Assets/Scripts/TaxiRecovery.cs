using UnityEngine;
using UnityEngine.InputSystem;

// Taksiyi kurtarma: R tuşuyla ya da taksi haritadan düşerse kendiliğinden,
// taksiyi en yakın yolun üstüne, yolun yönüne bakacak şekilde koyar.
// Taxi objesine eklenir.
[RequireComponent(typeof(Rigidbody))]
public class TaxiRecovery : MonoBehaviour
{
    public CityLayout layout;
    public PassengerManager passengers;     // Ekranda mesaj göstermek için (boş olabilir)
    public float fallHeight = -5f;          // Bu yüksekliğin altına düşerse kendiliğinden kurtar
    public float cooldown = 2f;             // İki kurtarma arasında en az bu kadar saniye

    Rigidbody rb;
    float lastRecovery = -10f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        bool keyPressed = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        bool fell = transform.position.y < fallHeight;

        if ((keyPressed || fell) && Time.time - lastRecovery > cooldown && Time.timeScale > 0f)
            Recover();
    }

    public void Recover()
    {
        lastRecovery = Time.time;

        // En yakın yol ve üzerindeki en yakın nokta
        RoadPoint rp = layout.Network.NearestPoint(transform.position);
        RoadEdge e = rp.edge;

        // Yolun iki yönünden, taksinin şu an baktığı yöne daha yakın olanı seç
        Vector3 dir = (e.b.position - e.a.position).normalized;
        if (Vector3.Dot(dir, transform.forward) < 0f) dir = -dir;

        // Sağ şeridin ortasına koy
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        Vector3 pos = rp.Position + right * (e.width / 4f) + Vector3.up * 0.5f;
        Quaternion rot = Quaternion.LookRotation(dir);

        // Hızı ve dönmeyi sıfırla, sonra yerleştir
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = pos;
        rb.rotation = rot;
        transform.SetPositionAndRotation(pos, rot);

        if (passengers != null) passengers.ShowMessage("Taksi yola geri alındı");
    }
}
