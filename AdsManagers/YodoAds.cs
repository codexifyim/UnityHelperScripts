using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yodo1.MAS;

public class YodoAds : MonoBehaviour
{
    private static YodoAds instance;
    public static YodoAds Instance => instance;

    Yodo1U3dBannerAdView bannerAdView = null;

    private bool rewardGrantedThisCycle = false;
    private float nextRewardLoadAllowedTime = 0f;
    private const float rewardLoadCooldown = 5f;
    [SerializeField] private string currentScene = "";

    public event Action OnRewardedAdCompleted;



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

    void Start()
    {

        // LoadBanner();

        Yodo1U3dMasCallback.OnAppEnterForegroundEvent += () =>
        {
            Debug.Log(Yodo1U3dMas.TAG + ": The game has entered the foreground");
        };


        Yodo1U3dMasCallback.OnUmpCompletionEvent += (Yodo1U3dAdError error) =>
        {
            if (error == null)
                Debug.Log(Yodo1U3dMas.TAG + "OnUmpCompletionEvent success");
            else
                Debug.Log(Yodo1U3dMas.TAG + "OnUmpCompletionEvent with error " + error);
        };


        Yodo1U3dMasCallback.OnSdkInitializationEvent += (Yodo1MasSdkConfiguration config, Yodo1U3dAdError error) =>
        {
            if (config == null)
            {
                Debug.Log(Yodo1U3dMas.TAG + " SDK Init failed: " + error);
                return;
            }


            Debug.Log(Yodo1U3dMas.TAG + " SDK Init success: " + config);
            Yodo1U3dMas.SetUserIdentifier(SystemInfo.deviceUniqueIdentifier);


            // InitializeBanner();
            InitializeInterstitial();
            InitializeRewarded();


            LoadInterstitialAds();
            LoadRewarded();
            LoadBanner();


            Invoke(nameof(ShowBanner), 1f); // Delay banner show to ensure it's stable
            // Invoke(nameof(LoadRewarded), 2f); // Optional second attempt to load rewarded
        };


        var userPrivacyConfig = new Yodo1MasUserPrivacyConfig()
            .titleBackgroundColor(Color.white)
            .titleTextColor(Color.black)
            .contentBackgroundColor(Color.white)
            .contentTextColor(Color.black)
            .buttonBackgroundColor(Color.yellow)
            .buttonTextColor(Color.white);


        var buildConfig = new Yodo1AdBuildConfig()
            .enableUserPrivacyDialog(true)
            .userPrivacyConfig(userPrivacyConfig)
            .enableATTAuthorization(true);


        Yodo1U3dMas.SetAdBuildConfig(buildConfig);
        Yodo1U3dMas.InitializeMasSdk();


    }

    //banner
    void LoadBanner()
    {
        bannerAdView = new Yodo1U3dBannerAdView(Yodo1U3dBannerAdSize.Banner, 
                        Yodo1U3dBannerAdPosition.BannerTop | 
                        Yodo1U3dBannerAdPosition.BannerLeft);

        // bannerAdView.LoadAd();
        // bannerAdView.Show();
    }

    void ShowBanner()
    {
        if (bannerAdView == null) return;

        bannerAdView.LoadAd();
        bannerAdView.Show();
    }
    public void HideBanner()
    {
        if (bannerAdView == null) return;

        bannerAdView.Hide();
        bannerAdView.Destroy();
        bannerAdView = null;


    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += (scene, _) =>
        {
            if (scene.name != currentScene)
            {
                currentScene = scene.name;
                Invoke(nameof(LoadBanner), 1f); // Re-show banner after scene load with slight delay
            }
        };

        Yodo1U3dRewardAd.GetInstance().OnAdEarnedEvent += OnRewardAdEarnedEvent;

    }

    private void OnDisable()
    {


        Yodo1U3dRewardAd.GetInstance().OnAdEarnedEvent -= OnRewardAdEarnedEvent;

    }

    public void ShowUmpForExistingUser()
    {
        var cfg = Yodo1U3dMas.GetSdkConfiguration();
        if (cfg == null) return;


        if (cfg.ConsentFlowUserGeography == Yodo1MasConsentFlowUserGeography.Gdpr)
            Yodo1U3dMas.ShowUmpForExistingUser();
    }


    // private Yodo1U3dBannerAdView _banner;
    private void InitializeBanner() { }


    


    private void InitializeInterstitial()
    {
        var ad = Yodo1U3dInterstitialAd.GetInstance();
        ad.OnAdLoadFailedEvent += (_, err) => Invoke(nameof(LoadInterstitialAds), 5f);
    }


    public void LoadInterstitialAds() => Yodo1U3dInterstitialAd.GetInstance().LoadAd();
    public bool IsInterstitialLoaded() => Yodo1U3dInterstitialAd.GetInstance().IsLoaded();


    public void ShowInterstitialAds(string placement = null)
    {
        if (!IsInterstitialLoaded()) return;
        if (string.IsNullOrEmpty(placement)) Yodo1U3dInterstitialAd.GetInstance().ShowAd();
        else Yodo1U3dInterstitialAd.GetInstance().ShowAd(placement);
    }


    private void InitializeRewarded()
    {
        var ad = Yodo1U3dRewardAd.GetInstance();
        // ad.OnAdEarnedEvent += (_) =>
        // {
        //     if (!rewardGrantedThisCycle)
        //     {
        //         rewardGrantedThisCycle = true;

        //         // OnRewardAdEarnedEvent(ad);
        //     }
        // };


        ad.OnAdClosedEvent += (_) =>
        {
            rewardGrantedThisCycle = false;
            LoadRewarded();
        };


        ad.OnAdLoadFailedEvent += (_, err) => Invoke(nameof(LoadRewarded), 5f);
    }


    public void LoadRewarded()
    {
        Yodo1U3dRewardAd.GetInstance().LoadAd();
    }


    public bool IsRewardedLoaded() => Yodo1U3dRewardAd.GetInstance().IsLoaded();


    public void ShowRewardedAds()
    {
        if (!IsRewardedLoaded()) return;

        Yodo1U3dRewardAd.GetInstance().ShowAd();
    }


    private void OnRewardAdEarnedEvent(Yodo1U3dRewardAd ad)
    {
        Debug.Log("[Yodo1 Mas] Reward ad earned");

        // OnRewardedAdCompleted();
        OnRewardedAdCompleted?.Invoke();

    }


    private void OnDestroy()
    {
        // HideBanner();
    }
}

