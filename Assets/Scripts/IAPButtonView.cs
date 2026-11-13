using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPButtonView : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text price;
    [SerializeField] private TMP_Text detail;

    public void OnProductFetched(Product product)
    {
        if (product == null) return;
        if (title) title.text = product.metadata.localizedTitle;
        if (price) price.text = product.metadata.localizedPriceString;
        if (detail) detail.text = product.metadata.localizedDescription;
    }
}
