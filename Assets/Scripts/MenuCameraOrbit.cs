using UnityEngine;

// Ana menünün arka planı: kamera şehrin etrafında yavaşça döner.
// MainMenu sahnesindeki Main Camera'ya eklenir.
public class MenuCameraOrbit : MonoBehaviour
{
    public Vector3 center = Vector3.zero;   // Şehrin ortası
    public float radius = 170f;             // Merkeze uzaklık
    public float height = 70f;              // Yerden yükseklik
    public float degreesPerSecond = 3f;     // Dönüş hızı (yavaş olsun, dikkat dağıtmasın)
    public float lookDown = 10f;            // Merkezin ne kadar altına bakılsın (m)

    float angle;

    void Update()
    {
        angle += degreesPerSecond * Time.deltaTime;

        // Çember üzerindeki konum: açıyı radyana çevirip sinüs/kosinüs ile hesapla
        float rad = angle * Mathf.Deg2Rad;
        transform.position = center + new Vector3(Mathf.Cos(rad) * radius, height, Mathf.Sin(rad) * radius);
        transform.LookAt(center + Vector3.down * lookDown);
    }
}
