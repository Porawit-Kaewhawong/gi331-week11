using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Workshop-only reward fulfillment for Unity IAP 5.5.
/// This component does not talk to the app store; IAPStore handles orders.
/// PlayerPrefs is NOT a secure/atomic storage system for real-money purchases.
/// </summary>
public class IAPRewardHandler : MonoBehaviour
{
    [Serializable]
    public struct PurchaseItem
    {
        public string ProductId;
        public int Quantity;

        public PurchaseItem(string productId, int quantity)
        {
            ProductId = productId;
            Quantity = quantity;
        }
    }

    [Header("Consumable")]
    [SerializeField] private TMP_Text coinText;
    [Min(1)] [SerializeField] private int coinsPerPack = 100;

    [Header("Non-Consumable")]
    [SerializeField] private GameObject adsPurchasedWindow;
    [SerializeField] private GameObject adsBanner;

    [Header("Subscription (workshop display only)")]
    [SerializeField] private GameObject subActivatedWindow;
    [SerializeField] private GameObject premiumLogo;

    private const string CoinsKey = "totalCoins";
    private const string RemoveAdsKey = "removeAdsOwned";
    private const string TransactionKeyPrefix = "iap_handled_";

    // Never persist 'true' permanently for subscriptions. Real status must come from the store.
    private bool subscriptionActiveThisSession;

    private void Awake()
    {
        RefreshUI();
    }

    /// <summary>
    /// Grants every item in a transaction and records a demo-only local transaction marker.
    /// Returns true if the transaction was handled already OR if it was granted now.
    /// Returns false on invalid data; IAPStore must NOT confirm such an order.
    /// </summary>
    public bool TryGrantTransaction(
        string transactionKey,
        IReadOnlyList<PurchaseItem> items,
        string coinsProductId,
        string removeAdsProductId,
        string battlePassProductId)
    {
        if (string.IsNullOrWhiteSpace(transactionKey) || items == null || items.Count == 0)
        {
            Debug.LogError("IAP reward: transaction ID or items missing.");
            return false;
        }

        string processedKey = TransactionKeyPrefix + transactionKey;
        if (PlayerPrefs.GetInt(processedKey, 0) == 1)
        {
            // A pending order can be delivered again if confirmation was interrupted.
            Debug.Log($"IAP reward: already handled {transactionKey}; skipping duplicate grant.");
            RefreshUI();
            return true;
        }

        // Validate all line items BEFORE changing local save data.
        long newCoins = PlayerPrefs.GetInt(CoinsKey, 0);
        bool removeAds = false;
        bool activateBattlePass = false;

        for (int i = 0; i < items.Count; i++)
        {
            PurchaseItem item = items[i];
            if (item.Quantity <= 0)
            {
                Debug.LogError($"IAP reward: invalid quantity for {item.ProductId}.");
                return false;
            }

            if (item.ProductId == coinsProductId)
            {
                newCoins += (long)coinsPerPack * item.Quantity;
                if (newCoins > int.MaxValue)
                {
                    Debug.LogError("IAP reward: coin balance exceeds Int32 range.");
                    return false;
                }
            }
            else if (item.ProductId == removeAdsProductId)
            {
                removeAds = true;
            }
            else if (item.ProductId == battlePassProductId)
            {
                activateBattlePass = true;
            }
            else
            {
                Debug.LogError($"IAP reward: unknown product ID '{item.ProductId}'.");
                return false;
            }
        }

        // Workshop-only local persistence; this is NOT atomic and can be edited by players.
        PlayerPrefs.SetInt(CoinsKey, (int)newCoins);
        if (removeAds)
            PlayerPrefs.SetInt(RemoveAdsKey, 1);
        PlayerPrefs.SetInt(processedKey, 1);
        PlayerPrefs.Save();

        if (activateBattlePass)
        {
            // Demonstration only. Do NOT use this flag to decide real subscription validity.
            subscriptionActiveThisSession = true;
        }

        RefreshUI();
        Debug.Log($"IAP reward: granted transaction {transactionKey}.");
        return true;
    }

    /// <summary>Restore Remove Ads after IAPStore has verified an owned order with the store.</summary>
    public void RestoreRemoveAdsFromStore()
    {
        if (PlayerPrefs.GetInt(RemoveAdsKey, 0) == 0)
        {
            PlayerPrefs.SetInt(RemoveAdsKey, 1);
            PlayerPrefs.Save();
        }
        RefreshUI();
    }

    /// <summary>
    /// Call ONLY with a subscription status validated through the store/backend.
    /// IAPStore in this workshop does not yet implement that validation.
    /// </summary>
    public void SetVerifiedSubscriptionActive(bool active)
    {
        subscriptionActiveThisSession = active;
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (coinText != null)
            coinText.text = PlayerPrefs.GetInt(CoinsKey, 0).ToString();

        bool ownsRemoveAds = PlayerPrefs.GetInt(RemoveAdsKey, 0) == 1;
        if (adsPurchasedWindow != null)
            adsPurchasedWindow.SetActive(ownsRemoveAds);
        if (adsBanner != null)
            adsBanner.SetActive(!ownsRemoveAds);

        if (subActivatedWindow != null)
            subActivatedWindow.SetActive(subscriptionActiveThisSession);
        if (premiumLogo != null)
            premiumLogo.SetActive(subscriptionActiveThisSession);
    }
}
