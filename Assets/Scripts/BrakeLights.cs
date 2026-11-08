using UnityEngine;

// Taksinin arka stop lambaları. Normalde hafif yanar, frende parlar.
// Taxi objesine eklenir. Lambaları oyun başlarken kendisi oluşturur.
[RequireComponent(typeof(TaxiController))]
public class BrakeLights : MonoBehaviour
{
    [Header("Malzeme")]
    [Tooltip("Emission'ı açık bir malzeme. Şehirdeki SehirMalzemesi kullanılabilir.")]
    public Material baseMaterial;

    [Header("Konum (taksiye göre)")]
    public Vector3 leftOffset = new Vector3(-0.615f, 0.47f, -2.01f);
    public Vector3 rightOffset = new Vector3(0.615f, 0.47f, -2.01f);
    public Vector3 lampSize = new Vector3(0.35f, 0.12f, 0.05f);

    [Header("Parlaklık")]
    public Color lampColor = new Color(1f, 0.08f, 0.05f);
    public float idleGlow = 0.6f;     // Normal sürüşte (arka lamba)
    public float brakeGlow = 3f;      // Frende (stop lambası)

    TaxiController taxi;
    Material lampMaterial;
    Light glowLight;                  // Arkadaki yola kırmızı ışık vurması için
    bool braking;

    void Start()
    {
        taxi = GetComponent<TaxiController>();

        lampMaterial = baseMaterial != null
            ? new Material(baseMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        lampMaterial.SetColor("_BaseColor", lampColor);
        lampMaterial.EnableKeyword("_EMISSION");

        CreateLamp("StopLambasi_Sol", leftOffset);
        CreateLamp("StopLambasi_Sag", rightOffset);

        // Spot ışık: arkaya ve hafif aşağıya, yola bakıyor. Önceki "nokta" ışık her yöne
        // yayıldığı için taksinin kendisini de kırmızıya boyuyordu.
        GameObject lightObj = new GameObject("StopIsigi");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = (leftOffset + rightOffset) / 2f + Vector3.back * 0.1f;
        lightObj.transform.localRotation = Quaternion.Euler(25f, 180f, 0f);   // Arkaya ve 25° aşağı
        glowLight = lightObj.AddComponent<Light>();
        glowLight.type = LightType.Spot;
        glowLight.spotAngle = 90f;
        glowLight.color = lampColor;
        glowLight.range = 7f;
        glowLight.shadows = LightShadows.None;

        SetBraking(false);
    }

    void Update()
    {
        // Sadece değiştiği anda güncelle (her karede malzeme değiştirmek gereksiz)
        if (taxi.IsBraking != braking) SetBraking(taxi.IsBraking);
    }

    void SetBraking(bool on)
    {
        braking = on;
        lampMaterial.SetColor("_EmissionColor", lampColor * (on ? brakeGlow : idleGlow));
        glowLight.intensity = on ? 2f : 0f;
    }

    void CreateLamp(string lampName, Vector3 offset)
    {
        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lamp.name = lampName;
        Destroy(lamp.GetComponent<Collider>());    // Çarpışmaya karışmasın
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = offset;
        lamp.transform.localScale = lampSize;
        lamp.GetComponent<Renderer>().sharedMaterial = lampMaterial;
    }

    // EDİTÖR YARDIMI: Taxi seçiliyken Scene penceresinde lambaların yerini kırmızı kutularla gösterir.
    // Böylece Play'e basmadan konumları ayarlayabilirsin.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(leftOffset, lampSize);
        Gizmos.DrawCube(rightOffset, lampSize);
    }
}
