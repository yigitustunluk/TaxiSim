using UnityEngine;
using System.Collections;

// Şehrin kaldırımlarına yayalar yerleştirir. GameManager'a eklenir.
public class PedestrianSpawner : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public CityGenerator city;
    public GameObject[] characterModels;               // Kenney karakter FBX dosyaları
    public RuntimeAnimatorController animatorController; // PedestrianAnimator

    [Header("Ayarlar")]
    public int count = 60;
    public float modelScale = 1f;          // Karakterler küçük/büyük görünürse buradan ayarla
    public float sidewalkInset = 1.6f;     // Kaldırım kenarından ne kadar içeride yürüsünler
    public float sidewalkHeight = 0.2f;    // Kaldırımın üst yüzeyi

    IEnumerator Start()
    {
        // Şehir üreticisinin blokları oluşturmasını bekle (o da Start'ta çalışıyor)
        yield return null;

        if (city == null || city.blockRects.Count == 0 || characterModels.Length == 0)
        {
            Debug.LogWarning("PedestrianSpawner: Şehir ya da karakter modelleri eksik.");
            yield break;
        }

        System.Random rng = new System.Random();
        for (int i = 0; i < count; i++)
        {
            Rect block = city.blockRects[rng.Next(city.blockRects.Count)];

            // Boş bir obje: Pedestrian script'i bunun üstünde, karakter modeli onun içinde
            GameObject root = new GameObject("Yaya_" + i);
            root.transform.SetParent(transform);

            GameObject model = Instantiate(characterModels[rng.Next(characterModels.Length)], root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * modelScale;

            // Modelin Animator'ına bizim animasyon şemamızı ver
            Animator anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = animatorController;
            anim.applyRootMotion = false;   // Hareketi animasyon değil, kod yapsın

            Pedestrian p = root.AddComponent<Pedestrian>();
            p.Init(block, sidewalkInset, sidewalkHeight, rng);
        }
    }
}
