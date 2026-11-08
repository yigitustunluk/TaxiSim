using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// Arayüze tek bir görsel dil uygular. Her sahnenin Canvas'ına eklenir.
// Butonları tek tek boyamak yerine, Canvas'ın altındaki BÜTÜN butonları (gizli olanlar ve
// prefab'dan sonradan üretilenler dahil) aynı renklere boyar. Renkleri değiştirmek isteyince
// tek bir yerden değiştirirsin, bütün oyun güncellenir.
public class ThemeApplier : MonoBehaviour
{
    [Header("Butonlar")]
    public Color buttonNormal = Hex("1E2430");
    public Color buttonHighlighted = Hex("2B3341");
    public Color buttonPressed = Hex("F2C200");
    public Color buttonText = Color.white;

    [Header("Tehlikeli Butonlar (adında 'Sil' geçenler)")]
    public Color dangerNormal = Hex("8E2B2B");
    public Color dangerHighlighted = Hex("A83434");

    [Header("Paneller ve Başlıklar (Inspector'dan sürükle)")]
    public Color panelColor = new Color(0.055f, 0.067f, 0.09f, 0.9f);   // 0E1117, %90
    public Image[] panels;
    public Color headingColor = Hex("F2C200");
    public TMP_Text[] headings;

    // Diğer script'ler Start'ta satır üretiyor (garaj, kayıt ekranı).
    // Bir kare bekleyip öyle boyarsak onlar da boyanmış olur.
    IEnumerator Start()
    {
        yield return null;
        Apply();
    }

    public void Apply()
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))   // true: gizli olanlar dahil
        {
            bool danger = b.name.Contains("Sil");

            // Buton rengi = resmin rengi x ColorBlock rengi. Resmi beyaz yapıp
            // asıl rengi ColorBlock'tan veriyoruz, böylece üstüne gelince/basınca renk değişir.
            if (b.image != null) b.image.color = Color.white;

            ColorBlock c = b.colors;
            c.normalColor = danger ? dangerNormal : buttonNormal;
            c.highlightedColor = danger ? dangerHighlighted : buttonHighlighted;
            c.pressedColor = buttonPressed;
            c.selectedColor = c.normalColor;
            c.disabledColor = new Color(c.normalColor.r, c.normalColor.g, c.normalColor.b, 0.35f);
            b.colors = c;

            foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true))
                t.color = buttonText;
        }

        if (panels != null)
            foreach (Image p in panels) if (p != null) p.color = panelColor;

        if (headings != null)
            foreach (TMP_Text h in headings) if (h != null) h.color = headingColor;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
