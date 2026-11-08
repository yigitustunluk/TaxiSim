using UnityEngine;

// Taksinin tekerleklerini döndürür (hıza göre) ve ön tekerlekleri direksiyonla kırar.
// Sadece GÖRÜNTÜ: sürüş fiziği hâlâ TaxiController'da, tekerleklerin fiziğe etkisi yok.
// Taxi objesine eklenir.
[RequireComponent(typeof(TaxiController))]
public class WheelVisuals : MonoBehaviour
{
    [Header("Tekerlekler (taxi modelinin içinden sürükle)")]
    public Transform frontLeft;
    public Transform frontRight;
    public Transform backLeft;
    public Transform backRight;

    [Header("Ayarlar")]
    public float maxSteerAngle = 30f;    // Ön tekerleklerin en fazla kırılma açısı
    public float steerSmooth = 10f;      // Direksiyonun ne kadar çabuk döneceği

    TaxiController taxi;
    Quaternion[] baseRotations = new Quaternion[4];
    Transform[] wheels;
    float radius;            // Tekerlek yarıçapı (metre)
    float spinAngle;         // Tekerleklerin toplam dönme açısı
    float steerAngle;

    void Start()
    {
        taxi = GetComponent<TaxiController>();
        wheels = new Transform[] { frontLeft, frontRight, backLeft, backRight };

        // Her tekerleğin başlangıç dönüşünü sakla. Sağ ve sol tekerlekler modelde
        // farklı yönlere bakıyor olabilir, onları bozmamak için bunun üstüne ekleyeceğiz.
        for (int i = 0; i < 4; i++)
            if (wheels[i] != null) baseRotations[i] = wheels[i].localRotation;

        // Yarıçapı modelden otomatik ölç: tekerleğin görünen yüksekliğinin yarısı
        Renderer r = frontLeft != null ? frontLeft.GetComponent<Renderer>() : null;
        radius = r != null ? r.bounds.extents.y : 0.4f;
    }

    void Update()
    {
        // Kayma olmadan yuvarlanan bir tekerlek: gidilen yol = yarıçap x dönme açısı (radyan)
        spinAngle += taxi.ForwardSpeed / radius * Mathf.Rad2Deg * Time.deltaTime;
        spinAngle %= 360f;   // Sayı sonsuza büyümesin

        steerAngle = Mathf.Lerp(steerAngle, taxi.Steer * maxSteerAngle, steerSmooth * Time.deltaTime);

        Quaternion spin = Quaternion.Euler(spinAngle, 0f, 0f);    // Aks etrafında dönüş
        Quaternion steer = Quaternion.Euler(0f, steerAngle, 0f);  // Direksiyon

        // Sıra önemli: önce tekerleğin kendi duruşu, sonra aks dönüşü, en son direksiyon.
        // Dönüşler modelin (arabanın) eksenlerine göre uygulanır, tekerleğinkine göre değil.
        for (int i = 0; i < 4; i++)
        {
            if (wheels[i] == null) continue;
            bool isFront = i < 2;
            wheels[i].localRotation = (isFront ? steer : Quaternion.identity) * spin * baseRotations[i];
        }
    }
}
