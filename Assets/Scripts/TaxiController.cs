using UnityEngine;
using UnityEngine.InputSystem;

// Arcade tarzı taksi kontrolü.
// Gerçekçi tekerlek fiziği (WheelCollider) yerine Rigidbody'ye doğrudan kuvvet uygular.
// Daha kolay ayarlanır ve taksi oyunu hissine daha uygundur.
[RequireComponent(typeof(Rigidbody))]
public class TaxiController : MonoBehaviour
{
    [Header("Motor")]
    public float acceleration = 8.5f;        // İleri ivme (m/s²)
    public float reverseAcceleration = 6f;   // Geri vites ivmesi
    public float maxSpeed = 25f;             // m/s  (25 m/s ≈ 90 km/s)
    public float brakeForce = 20f;           // Fren gücü

    [Header("Direksiyon")]
    public float turnSpeed = 80f;            // Saniyede kaç derece döner
    public float gripStrength = 6f;          // Yüksek = az kayma, düşük = drift

    [Header("Zemin Kontrolü")]
    public float groundCheckDistance = 1f; // Aracın altına atılan ışın uzunluğu

    Rigidbody rb;
    float throttleInput;   // -1 (geri) ile +1 (ileri) arası
    float steerInput;      // -1 (sol) ile +1 (sağ) arası
    bool brakeInput;
    bool isGrounded;

    // Taksi şu an kaldırımın üstünde mi? PassengerManager bunu okuyup ceza verir.
    public bool IsOnSidewalk { get; private set; }

    // Hızı km/s olarak verir. İleride taksimetre ve arayüzde kullanacağız.
    public float SpeedKmh => rb.linearVelocity.magnitude * 3.6f;
    public float Throttle => throttleInput;              // Motor sesi için: gaza basılıyor mu
    public float MaxSpeedKmh => maxSpeed * 3.6f;
    public float Steer => steerInput;                                       // -1 sol, +1 sağ
    public float ForwardSpeed => Vector3.Dot(rb.linearVelocity, transform.forward); // m/s, geri giderken eksi
    // Fren yapılıyor mu? Space'e basılıyorsa ya da ileri giderken S'ye basılıyorsa
    public bool IsBraking => brakeInput || (throttleInput < 0f && ForwardSpeed > 0.5f);

    // ÇARPIŞMA OLAYI (event): Taksi sert bir şeye çarpınca bunu "duyurur".
    // Başka script'ler (PassengerManager gibi) bu duyuruya abone olup tepki verir.
    // Böylece taksi, kimin dinlediğini bilmek zorunda kalmaz.
    public System.Action<float> OnCrash;
    public float crashMinImpact = 4f;   // Bundan yavaş çarpmalar sayılmaz (m/s)
    float lastCrashTime = -10f;

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time - lastCrashTime < 1f) return;           // Aynı çarpışmayı iki kez sayma
        if (collision.contactCount == 0) return;
        if (collision.GetContact(0).normal.y > 0.5f) return;  // Zemine değmek çarpışma değildir

        float impact = collision.relativeVelocity.magnitude;
        if (impact < crashMinImpact) return;

        lastCrashTime = Time.time;
        OnCrash?.Invoke(impact);   // "?." = dinleyen yoksa hata verme
    }

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
        // Işın bir şeye çarparsa, neye çarptığını "hit" içine yazar.
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                                     groundCheckDistance, ~0, QueryTriggerInteraction.Ignore);
        IsOnSidewalk = isGrounded && hit.collider.GetComponent<Sidewalk>() != null;
        if (!isGrounded) return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, forward); // İleri yöndeki hız

        // --- GAZ / GERİ ---
        if (throttleInput > 0f && forwardSpeed < maxSpeed)
        {
            // Gerçek arabalar gibi: kalkışta güçlü, azami hıza yaklaştıkça zayıflayan ivme.
            // Hız 0'dayken tam güç, azami hızdayken %25 güç.
            float speedRatio = Mathf.Clamp01(forwardSpeed / maxSpeed);
            float accelFactor = Mathf.Lerp(1f, 0.25f, speedRatio);
            rb.AddForce(forward * acceleration * accelFactor, ForceMode.Acceleration);
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
}
