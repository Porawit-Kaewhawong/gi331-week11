using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// GI331 workshop - Unity IAP 5.5+ / Fake App Store / code-defined local catalog.
/// IAPStore owns store requests; IAPRewardHandler owns in-game rewards.
/// Workshop only: do not use PlayerPrefs-based fulfillment in production.
/// </summary>
public class IAPStore : MonoBehaviour
{
    [Header("Products (keep IDs consistent with your original workshop)")]
    [SerializeField] private string coinsProductId = "100coins";
    [SerializeField] private string removeAdsProductId = "remove_ads";
    [SerializeField] private string battlePassProductId = "battle_pass";

    [Header("Rewards")]
    [SerializeField] private IAPRewardHandler rewardHandler;

    [Header("Product UI (optional)")]
    [SerializeField] private IAPButtonView coinsView;
    [SerializeField] private IAPButtonView removeAdsView;
    [SerializeField] private IAPButtonView battlePassView;

    private PurchasingController controller;
    private string storeName;
    private bool readyToBuy;

    private void Awake()
    {
        if (rewardHandler == null)
            rewardHandler = GetComponent<IAPRewardHandler>();
    }

    private async void Start()
    {
        if (rewardHandler == null)
        {
            Debug.LogError("IAPStore: Assign IAPRewardHandler in the Inspector.");
            return;
        }

        try
        {
            // 1. Select the platform-native store (Fake App Store in the Editor).
            storeName = UnityIAPServices.GetDefaultStore();
            controller = new PurchasingController(storeName);

            // 2. Register every callback BEFORE connecting.
            controller.OnStoreConnected += OnStoreConnected;
            controller.OnStoreDisconnected += OnStoreDisconnected;
            controller.OnProductsFetched += OnProductsFetched;
            controller.OnProductsFetchFailed += OnProductsFetchFailed;
            controller.OnPurchasesFetched += OnPurchasesFetched;
            controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            controller.OnPurchasePending += OnPurchasePending;
            controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
            controller.OnPurchaseFailed += OnPurchaseFailed;

            // 3. Connect to the store.
            await controller.Connect();

            // 4. Define the same three kinds of products as the original workshop.
            // These are code-defined LOCAL catalog entries, not Dashboard listings.
            var catalog = new CatalogProvider();
            catalog.AddProduct(coinsProductId, ProductType.Consumable);
            catalog.AddProduct(removeAdsProductId, ProductType.NonConsumable);
            catalog.AddProduct(battlePassProductId, ProductType.Subscription);

            // 5. Fetch product data from the selected store.
            controller.FetchProductsWithNoRetries(
                catalog.GetProducts(storeName), storeName);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void OnStoreConnected(string source)
    {
        Debug.Log($"IAP: Connected to {source}.");
    }

    private void OnStoreDisconnected(
        string source, StoreConnectionFailureDescription failure)
    {
        readyToBuy = false;
        Debug.LogError($"IAP: Store disconnected ({source}): {failure.message}");
    }

    private void OnProductsFetched(string source, List<Product> products)
    {
        Debug.Log($"IAP: Fetched {products.Count} products from {source}.");

        foreach (Product product in products)
        {
            string id = product.definition.id;
            if (id == coinsProductId && coinsView != null)
                coinsView.OnProductFetched(product);
            else if (id == removeAdsProductId && removeAdsView != null)
                removeAdsView.OnProductFetched(product);
            else if (id == battlePassProductId && battlePassView != null)
                battlePassView.OnProductFetched(product);
        }

        // 6. Restore ownership and handle previously pending orders.
        controller.FetchPurchases(source);
    }

    private void OnProductsFetchFailed(string source, ProductFetchFailed failure)
    {
        readyToBuy = false;
        Debug.LogError($"IAP: Products fetch failed ({source}): {failure.FailureReason}");
    }

    private void OnPurchasesFetched(string source, Orders orders)
    {
        // Restore only non-consumable ownership. Do not grant coins again.
        foreach (var order in orders.ConfirmedOrders)
        {
            foreach (var item in order.CartOrdered.Items())
            {
                if (item.Product.definition.id == removeAdsProductId)
                    rewardHandler.RestoreRemoveAdsFromStore();
            }
        }

        // Subscriptions require a separate current-status verification.
        readyToBuy = true;
        Debug.Log($"IAP: Purchase history loaded ({source}). Ready to buy.");
    }

    private void OnPurchasesFetchFailed(
        string source, PurchasesFetchFailureDescription failure)
    {
        readyToBuy = false;
        Debug.LogWarning(
            $"IAP: Purchase-history fetch failed ({source}): {failure.FailureReason} - {failure.Message}");
    }

    private void OnPurchasePending(string source, PendingOrder order)
    {
        string transactionId = order.Info?.TransactionID;
        if (string.IsNullOrEmpty(transactionId))
        {
            Debug.LogError("IAP: Missing transaction ID. Order remains pending.");
            return;
        }

        // 7. Let the game fulfill the order before confirming it.
        var items = new List<IAPRewardHandler.PurchaseItem>();
        foreach (var item in order.CartOrdered.Items())
            items.Add(new IAPRewardHandler.PurchaseItem(
                item.Product.definition.id, item.Quantity));

        bool granted = rewardHandler.TryGrantTransaction(
            source + ":" + transactionId, items,
            coinsProductId, removeAdsProductId, battlePassProductId);

        // 8. Confirm ONLY if the reward has been saved/handled.
        if (granted)
            controller.ConfirmPurchase(order);
    }

    private void OnPurchaseConfirmed(string source, Order order)
    {
        if (order is FailedOrder failed)
            Debug.LogError($"IAP: Confirmation failed: {failed.FailureReason} - {failed.Details}");
        else
            Debug.Log($"IAP: Purchase confirmed ({source}).");
    }

    private void OnPurchaseFailed(string source, FailedOrder failed)
    {
        Debug.LogWarning($"IAP: Purchase failed ({source}): {failed.FailureReason} - {failed.Details}");
    }

    public void BuyCoins100() => Buy(coinsProductId);
    public void BuyRemoveAds() => Buy(removeAdsProductId);
    public void BuyBattlePass() => Buy(battlePassProductId);

    private void Buy(string productId)
    {
        if (!readyToBuy || controller == null)
        {
            Debug.LogWarning("IAP: Products / purchase history not ready yet.");
            return;
        }

        controller.PurchaseProduct(productId, storeName);
    }

    private void OnDestroy()
    {
        if (controller == null) return;
        controller.OnStoreConnected -= OnStoreConnected;
        controller.OnStoreDisconnected -= OnStoreDisconnected;
        controller.OnProductsFetched -= OnProductsFetched;
        controller.OnProductsFetchFailed -= OnProductsFetchFailed;
        controller.OnPurchasesFetched -= OnPurchasesFetched;
        controller.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
        controller.OnPurchasePending -= OnPurchasePending;
        controller.OnPurchaseConfirmed -= OnPurchaseConfirmed;
        controller.OnPurchaseFailed -= OnPurchaseFailed;
    }
}
