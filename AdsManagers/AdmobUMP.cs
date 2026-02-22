//------Google User Messaging Platform---------//
// also use UnityWebRequestExtensions.cs script along side this

using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using GoogleMobileAds.Api;
using GoogleMobileAds.Api.Mediation.UnityAds;
using GoogleMobileAds.Api.Mediation.IronSource;
using GoogleMobileAds.Ump.Api;
using GoogleMobileAds.Common;

public class AdmobUMP : MonoBehaviour
{
    

    private static AdmobUMP instance;
    public static AdmobUMP Instance => instance;


    [Header("AdMob Ad Unit IDs")] 
    private readonly string _rewardedAdUnitId = "";

    private readonly string _interstitialAdUnitId = "";
    private  readonly string _bannerAdUnitId = "";

    private RewardedAd _rewardedAd;
    private InterstitialAd _interstitialAd;
    private BannerView _bannerView;

    // State tracking
    private bool _isInitialized;
    public bool IsInitialized => _isInitialized;
    private bool _consentGiven;
    private bool _isLoadingAds;
    private Coroutine _bannerCoroutine;

    // Events for external subscribers
    public event Action OnRewardedAdCompleted;
    public event Action OnRewardedAdFailed;
    public event Action OnInterstitialAdClosed;
    public event Action<bool> OnConsentUpdated;

    private readonly TaskCompletionSource<bool> _initializationTcs = new();
    public Task InitializationComplete => _initializationTcs.Task;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start()
    {
        // Wait a frame to ensure everything is initialized
        await Task.Yield();

        // Check connectivity and start consent flow
        await InitializeAdsWithConsent();
    }


    

    private async Task<bool> IsInternetAvailableAsync()
    {
        // First quick check using Unity's built-in property
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("No internet connection detected.");
            return false;
        }

        // List of connectivity check endpoints in order of preference
        string[] checkUrls = new string[]
        {
            "https://connectivitycheck.gstatic.com/generate_204", // Best - returns 204
            // "http://cp.cloudflare.com/generate/_204",
            // "https://www.baidu.com",
            // "https://www.sina.com.cn",   
            "http://www.msftconnecttest.com/connecttest.txt", // Microsoft - returns text
            // "https://1.1.1.1", // Cloudflare DNS (very fast globally)
            // "https://www.cloudflare.com/",                          // Cloudflare CDN
            // "https://www.google.com"                                // Final fallback
        };

        foreach (string url in checkUrls)
        {
            try
            {
                using (UnityWebRequest request = UnityWebRequest.Head(url))
                {
                    request.timeout = 2;
                    await request.SendWebRequestAsync();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        // Debug.Log($"Internet check successful using: {url}");
                        return true;
                    }
                }
            }
            catch
            {
                // Silently fail and try next URL
            }
        }

        Debug.LogError("No internet connection detected.");
        return false;
    }

    private async Task WaitForInternetConnectionAsync(int maxAttempts = 6, float delaySeconds = 5f)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            if (await IsInternetAvailableAsync())
            {
                // Debug.Log("Internet connection established.");
                return;
            }

            // Debug.Log($"Waiting for internet connection... Attempt {i + 1}/{maxAttempts}");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        // Debug.LogWarning("Proceeding without internet connection after multiple attempts.");
    }

    // ===========================================================
    //   ███   UMP CONSENT (GDPR / CCPA) - Modern patterns
    // ===========================================================

    private async Task InitializeAdsWithConsent()
    {
        // Wait for internet connection (optional, can be removed if you want to proceed anyway)
        await WaitForInternetConnectionAsync(maxAttempts: 3);

        // Request consent
        var consentTask = RequestUserConsentAsync();
        await consentTask;

        // Initialize ads regardless of consent status
        InitializeMobileAds();
        // _initializationTcs?.SetResult(true);
    }

    private Task<bool> RequestUserConsentAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        // Debug.Log("📌 Requesting UMP Consent...");

        var request = new ConsentRequestParameters
        {
            // Set this to false if you're not located in the EEA or UK
            TagForUnderAgeOfConsent = false
        };

        ConsentInformation.Update(request, updateError =>
        {
            if (updateError != null)
            {
                Debug.LogError($"UMP Update Error: {updateError.Message}");
                _consentGiven = false;
                tcs.SetResult(false);
                return;
            }

            if (ConsentInformation.ConsentStatus == ConsentStatus.Required)
            {
                ConsentForm.Load((form, loadError) =>
                {
                    if (loadError != null)
                    {
                        Debug.LogError($"UMP Load Error: {loadError.Message}");
                        _consentGiven = false;
                        tcs.SetResult(false);
                        return;
                    }

                    form.Show(showError =>
                    {
                        if (showError != null)
                        {
                            Debug.LogError($"UMP Show Error: {showError.Message}");
                        }

                        FinalizeConsent();
                        tcs.SetResult(_consentGiven);
                    });
                });
            }
            else
            {
                FinalizeConsent();
                tcs.SetResult(_consentGiven);
            }
        });

        return tcs.Task;
    }

    private void FinalizeConsent()
    {
        _consentGiven = ConsentInformation.ConsentStatus == ConsentStatus.Obtained ||
                        ConsentInformation.ConsentStatus == ConsentStatus.NotRequired;

        // Debug.Log($"📝 Consent Given = {_consentGiven} (Status: {ConsentInformation.ConsentStatus})");

        // Update Unity Ads consent
        GoogleMobileAds.Mediation.UnityAds.Api.UnityAds.SetConsentMetaData("gdpr.consent", _consentGiven);
        GoogleMobileAds.Mediation.UnityAds.Api.UnityAds.SetConsentMetaData("privacy.consent", _consentGiven);

        OnConsentUpdated?.Invoke(_consentGiven);
    }

    // ===========================================================
    //   ███   ADMOB INITIALIZATION
    // ===========================================================

    private void InitializeMobileAds()
    {
        if (_isInitialized) return;
        

        // Request configuration
        var requestConfiguration = new RequestConfiguration
        {
            TagForChildDirectedTreatment = TagForChildDirectedTreatment.Unspecified,
            TagForUnderAgeOfConsent = TagForUnderAgeOfConsent.Unspecified,
            MaxAdContentRating = MaxAdContentRating.G
        };

        MobileAds.SetRequestConfiguration(requestConfiguration);

        MobileAds.Initialize(initStatus =>
        {
            _isInitialized = true;
            // Debug.Log("   AdMob initialized successfully.");

            // Log adapter status

            // Start loading ads
            StartLoadingAllAds();
        });
    }

    private void StartLoadingAllAds()
    {
        if (_isLoadingAds) return;
        _isLoadingAds = true;

        LoadRewardedAd();
        LoadInterstitialAd();
        LoadBannerAd();

    }

    // ===========================================================
    //   ███   REWARDED ADS (Enhanced with better error handling)
    // ===========================================================

    public void LoadRewardedAd()
    {
        if (string.IsNullOrEmpty(_rewardedAdUnitId))
        {
            Debug.LogError("  Rewarded Ad Unit ID is not set!");
            return;
        }

        var adRequest = new AdRequest();

        RewardedAd.Load(_rewardedAdUnitId, adRequest, (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"  Rewarded ad failed to load: {error.GetMessage()}");
                Debug.LogError($"Domain: {error.GetDomain()}, Code: {error.GetCode()}");

                // Retry with exponential backoff
                Invoke(nameof(LoadRewardedAd), GetRetryDelay());
                // OnRewardedAdAvailabilityChanged?.Invoke(); // Add this
                return;
            }

            _rewardedAd = ad;
            // Debug.Log("   Rewarded ad loaded successfully.");
            // RewardGivenAdmobClassic();
            RegisterRewardedAdEvents(_rewardedAd);
            // OnRewardedAdLoaded?.Invoke(); // Existing
            // OnRewardedAdAvailabilityChanged?.Invoke(); // Add this
        });
    }

    

    private void RegisterRewardedAdEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            // Thread-safe way to reload ads and trigger Unity events
            MobileAdsEventExecutor.ExecuteInUpdate(LoadRewardedAd);
        };
    }

    public void ShowRewardedAd()
    {
        ShowRewardedAd(null);
    }

    private bool _isShowingAd;

    public void ShowRewardedAd(Action onAdCompleted)
    {
        if (_isShowingAd) return;

        if (_rewardedAd != null && _rewardedAd.CanShowAd())
        {
            _isShowingAd = true;

            _rewardedAd.Show(_ =>
            {
                // // Debug.Log($"🏆 User rewarded: {reward.Amount} {reward.Type}");
                // onAdCompleted?.Invoke();
                // OnRewardedAdCompleted?.Invoke();
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    onAdCompleted?.Invoke();
                    OnRewardedAdCompleted?.Invoke();
                    _isShowingAd = false;
                });
            });
        }
        else
        {
            Debug.LogWarning("⚠️ Rewarded ad not ready. Loading now...");
            LoadRewardedAd();

            // Optionally show a loading indicator or retry after delay
            // StartCoroutine(RetryShowRewardedAd(onAdCompleted));
        }
    }

    // private IEnumerator RetryShowRewardedAd(Action onAdCompleted, int maxRetries = 3)
    // {
    //     int retryCount = 0;
    //     while (retryCount < maxRetries && (_rewardedAd == null || !_rewardedAd.CanShowAd()))
    //     {
    //         yield return new WaitForSeconds(1f);
    //         retryCount++;
    //     }

    //     if (_rewardedAd != null && _rewardedAd.CanShowAd())
    //     {
    //         _rewardedAd.Show((Reward reward) =>
    //         {
    //             // Debug.Log($"🏆 User rewarded: {reward.Amount} {reward.Type}");
    //             onAdCompleted?.Invoke();
    //             OnRewardedAdCompleted?.Invoke();
    //         });
    //     }
    //     else
    //     {
    //         Debug.LogError("  Failed to show rewarded ad after multiple retries");
    //     }
    // }

    // ===========================================================
    //   ███   INTERSTITIAL ADS (Enhanced)
    // ===========================================================

    public void LoadInterstitialAd()
    {
        if (string.IsNullOrEmpty(_interstitialAdUnitId))
        {
            Debug.LogError("  Interstitial Ad Unit ID is not set!");
            return;
        }

        var adRequest = new AdRequest();

        InterstitialAd.Load(_interstitialAdUnitId, adRequest, (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"  Interstitial failed to load: {error.GetMessage()}");
                Invoke(nameof(LoadInterstitialAd), GetRetryDelay());
                return;
            }

            _interstitialAd = ad;
            // Debug.Log("   Interstitial ad loaded successfully.");
            RegisterInterstitialAdEvents(_interstitialAd);
        });
    }

    private void RegisterInterstitialAdEvents(InterstitialAd ad)
    {
       

        ad.OnAdFullScreenContentClosed += () =>
        {
            // // Debug.Log("  Interstitial ad closed - Reloading");
            // LoadInterstitialAd();
            // OnInterstitialAdClosed?.Invoke();
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                // Debug.Log("Interstitial closed. Resuming game...");

                // Safe to interact with Unity objects here
                _isShowingInterstitial = false; // RESET THE GATE
                AudioListener.pause = false;    // RESUME AUDIO
                Time.timeScale = 1;
                OnInterstitialAdClosed?.Invoke();

                // Clean up and reload for next time
                LoadInterstitialAd();
            });
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            // Debug.LogError($"⚠️ Interstitial failed to show: {error.GetMessage()}");
            // LoadInterstitialAd();
            MobileAdsEventExecutor.ExecuteInUpdate(() => 
        {
            // Debug.LogError("Interstitial failed: " + error.GetMessage());
            _isShowingInterstitial = false; // RESET THE GATE
            AudioListener.pause = false;
            Time.timeScale = 1;
            LoadInterstitialAd();
        });

        };

    }

private bool _isShowingInterstitial = false;
    public void ShowInterstitialAd()
    {
        if (_interstitialAd != null && _interstitialAd.CanShowAd())
        {
            if (_isShowingInterstitial) return;

            _isShowingInterstitial = true;
            _interstitialAd.Show();
        }
        else
        {
            Debug.LogWarning("⚠️ Interstitial ad not ready. Loading...");
            LoadInterstitialAd();
        }
    }

    // ===========================================================
    //   ███   BANNER ADS (Modern implementation)
    // ===========================================================
    
    public void LoadBannerAd()
    {
        if (string.IsNullOrEmpty(_bannerAdUnitId)) return;

        // 1. Clean up old banner
        if (_bannerView != null) _bannerView.Destroy();

        
        AdSize adaptiveSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);

        // 3. Create the banner with the new size
        _bannerView = new BannerView(_bannerAdUnitId, adaptiveSize, AdPosition.Top);

        // 4. Load the ad
        AdRequest adRequest = new AdRequest();
        _bannerView.LoadAd(adRequest);
    }

    public IEnumerator ShowBannerWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowBanner();
    }

    public void ShowBanner()
    {
        if (_bannerView == null)
        {
            LoadBannerAd();
            // MyLoadBannerAd();
            StartCoroutine(ShowBannerWithDelay(1f));
            return;
        }

        _bannerView.Show();
        // Debug.Log("📌 Banner ad shown");
    }

    public void HideBanner()
    {
        if (_bannerView != null)
        {
            _bannerView.Hide();
            // Debug.Log("📌 Banner ad hidden");
        }
    }

    public void DestroyBanner()
    {
        if (_bannerView != null)
        {
            _bannerView.Destroy();
            _bannerView = null;
            // Debug.Log("📌 Banner ad destroyed");
        }
    }

    // ===========================================================
    //   ███   UTILITY METHODS
    // ===========================================================

    private float GetRetryDelay(int retryCount = 0)
    {
        // Exponential backoff: 2s, 4s, 8s, 16s, etc.
        return Mathf.Pow(2, Mathf.Clamp(retryCount, 0, 4)) * 2;
    }

    public bool IsRewardedAdReady()
    {
        return _rewardedAd != null && _rewardedAd.CanShowAd();
    }

    public bool IsInterstitialAdReady()
    {
        return _interstitialAd != null && _interstitialAd.CanShowAd();
    }


    private void OnDestroy()
    {
        // Clean up ads when object is destroyed
        _rewardedAd?.Destroy();
        _interstitialAd?.Destroy();
        DestroyBanner();
    }
}

