using UnityEngine;

// Taksiyi arkadan ve yukarıdan yumuşakça takip eden kamera.
public class CameraFollow : MonoBehaviour
{
    public Transform target;                             // Takip edilecek nesne (Taxi)
    public Vector3 offset = new Vector3(0f, 3.5f, -8f);  // Araca göre konum: yukarı ve geri
    public float followSmooth = 5f;                      // Yüksek = daha sıkı takip
    public float lookHeight = 1f;                        // Aracın ne kadar üstüne bakılacak

    // LateUpdate, tüm hareketler bittikten sonra çalışır. Kamera için doğru yer burası.
    void LateUpdate()
    {
        if (target == null) return;

        // Aracın yönüne göre offset'i döndürüyoruz. Aracın boyutu (Scale) hesaba katılmıyor.
        Vector3 desiredPosition = target.position + target.rotation * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition,
                                          followSmooth * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
