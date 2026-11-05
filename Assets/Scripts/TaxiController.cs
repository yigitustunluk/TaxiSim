using UnityEngine;
using UnityEngine.InputSystem;

// Arcade tarzı taksi kontrolü.
// Gerçekçi tekerlek fiziği (WheelCollider) yerine Rigidbody'ye doğrudan kuvvet uygular.
// Daha kolay ayarlanır ve taksi oyunu hissine daha uygundur.
[RequireComponent(typeof(Rigidbody))]
public class TaxiController : MonoBehaviour
{
    [Header("Motor")]
    public float acceleration = 12f;         // İleri ivme (m/s²)
    public float reverseAcceleration = 6f;   // Geri vites ivmesi
    public float maxSpeed = 25f;             // m/s  (25 m/s ≈ 90 km/s)
    public float brakeForce = 20f;           // Fren gücü

    [Header("Direksiyon")]
    public float turnSpeed = 80f;            // Saniyede kaç derece döner
    public float gripStrength = 6f;          // Yüksek = az kayma, düşük = drift

    [Header("Zemin Kontrolü")]
    public float groundCheckDistance = 0.7f; // Aracın altına atılan ışın uzunluğu

    Rigidbody rb;
    float throttleInput;   // -1 (geri) ile +1 (ileri) arası
    float steerInput;      // -1 (sol) ile +1 (sağ) arası
    bool brakeInput;
    bool isGrounded;

    // Hızı km/s olarak verir. İleride taksimetre ve arayüzde kullanacağız.
    public float SpeedKmh => rb.linearVelocity.magnitude * 3.6f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;  // Kamera titremesini önler
        rb.constraints = RigidbodyConstraints.FreezeRotationX   // Takla atmayı engeller
                       | RigidbodyConstraints.FreezeRotationZ;
    }

    // Update: her karede çalışır. Tuşları burada okuruz.
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        throttleInput = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)   throttleInput += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttleInput -= 1f;

        steerInput = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerInput += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  steerInput -= 1f;

        brakeInput = kb.spaceKey.isPressed;
    }

    // FixedUpdate: fizik adımlarında çalışır. Kuvvetleri burada uygularız.
    void FixedUpdate()
    {
        // Çarpışmaların aracı kendi etrafında döndürmesini engeller.
        // Dönüşü zaten direksiyon kodu (MoveRotation) yapıyor, fiziğin ayrıca döndürmesine gerek yok.
        rb.angularVelocity = Vector3.zero;

        // Araç yerde mi? Havadayken gaz ve direksiyon çalışmaz.
        isGrounded = Physics.Raycast(transform.position, Vector3.down,
                                     groundCheckDistance, ~0, QueryTriggerInteraction.Ignore);
        if (!isGrounded) return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, forward); // İleri yöndeki hız

        // --- GAZ / GERİ ---
        if (throttleInput > 0f && forwardSpeed < maxSpeed)
        {
            rb.AddForce(forward * acceleration, ForceMode.Acceleration);
        }
        else if (throttleInput < 0f)
        {
            if (forwardSpeed > 0.5f)
                rb.AddForce(-forward * brakeForce, ForceMode.Acceleration); // İleri giderken S = fren
            else if (forwardSpeed > -maxSpeed * 0.3f)
                rb.AddForce(-forward * reverseAcceleration, ForceMode.Acceleration); // Geri vites
        }

        // --- FREN (Space) ---
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (brakeInput && flatVelocity.magnitude > 0.1f)
        {
            rb.AddForce(-flatVelocity.normalized * brakeForce, ForceMode.Acceleration);
        }

        // --- DİREKSİYON ---
        // Duran araç kendi etrafında dönmesin diye dönüşü hıza bağlıyoruz.
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f);
        float direction = forwardSpeed >= 0f ? 1f : -1f; // Geri giderken direksiyon ters çalışır
        float turnAmount = steerInput * turnSpeed * speedFactor * direction * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));

        // --- YOL TUTUŞ ---
        // Yana doğru kaymayı azaltır. Bu olmazsa araç buz üstündeymiş gibi kayar.
        float sideSpeed = Vector3.Dot(rb.linearVelocity, right);
        rb.AddForce(-right * sideSpeed * gripStrength, ForceMode.Acceleration);
    }

    // --- TEŞHİS EKRANI ---
    // Oyun ekranının sol üstüne aracın durumunu yazar. Sorun çözülünce sileceğiz.
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22;
        style.normal.textColor = Color.white;

        string klavye = Keyboard.current != null ? "BULUNDU" : "YOK";
        string yerde = isGrounded ? "EVET" : "HAYIR";

        GUI.Box(new Rect(10, 10, 360, 150), "");
        GUI.Label(new Rect(20, 15, 340, 30), "Klavye: " + klavye, style);
        GUI.Label(new Rect(20, 50, 340, 30), "Gaz: " + throttleInput, style);
        GUI.Label(new Rect(20, 85, 340, 30), "Yerde mi: " + yerde, style);
        GUI.Label(new Rect(20, 120, 340, 30), "Hız: " + SpeedKmh.ToString("0") + " km/s", style);
    }
}
