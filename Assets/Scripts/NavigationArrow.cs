using UnityEngine;

// Taksinin üstünde süzülen ve hedefi gösteren bir ok.
// Yeşil: yolcuya git. Turuncu: yolcuyu varış noktasına götür.
public class NavigationArrow : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public Transform taxi;
    public PassengerManager passengers;

    [Header("Görünüm")]
    public float height = 2.6f;        // Okun taksiden ne kadar yukarıda duracağı
    public float turnSmooth = 8f;      // Yüksek = ok hedefe daha hızlı döner

    Transform arrow;
    Material arrowMaterial;

    void Start()
    {
        BuildArrow();
    }

    // LateUpdate: taksi bu karedeki hareketini bitirdikten sonra çalışır
    void LateUpdate()
    {
        if (taxi == null || passengers == null) return;

        // Oku taksinin üstüne taşı. Taksiye bağlı (child) değil, çünkü taksinin Scale'i oku bozardı.
        arrow.position = taxi.position + Vector3.up * height;

        // Hedefe doğru yatay yönü bul ve oku yumuşakça o yöne çevir
        Vector3 direction = passengers.TargetPosition - taxi.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            arrow.rotation = Quaternion.Slerp(arrow.rotation, targetRotation, turnSmooth * Time.deltaTime);
        }

        // Rengi göreve göre ayarla
        Color c = passengers.IsCarryingPassenger ? new Color(1f, 0.55f, 0f) : Color.green;
        arrowMaterial.SetColor("_BaseColor", c);
        arrowMaterial.color = c;
    }

    // Oku iki küpten oluşturur: bir gövde ve 45 derece döndürülmüş bir uç
    void BuildArrow()
    {
        arrow = new GameObject("YonOku").transform;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        arrowMaterial = new Material(shader);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.transform.SetParent(arrow);
        shaft.transform.localPosition = new Vector3(0f, 0f, -0.2f);
        shaft.transform.localScale = new Vector3(0.3f, 0.2f, 1.4f);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.transform.SetParent(arrow);
        head.transform.localPosition = new Vector3(0f, 0f, 0.6f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        head.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);

        foreach (Renderer r in arrow.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = arrowMaterial;

        // Okun taksiye çarpmaması için çarpışmaları kapat
        foreach (Collider col in arrow.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }
}
