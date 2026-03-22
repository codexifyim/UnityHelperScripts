using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class AudioManagerUpdated : MonoBehaviour
{
    public static AudioManagerUpdated Instance { get; private set; }

    #region Serialized Fields
    [Header("Audio Sources")]
    [Tooltip("Dedicated source for background music (looping)")]
    [SerializeField] private AudioSource musicSource;
    
    [Tooltip("Primary source for short SFX (non-looping)")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Volume Settings")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip audioClipButton;
    [SerializeField] private AudioClip audioClipGameover;
    [SerializeField] private AudioClip audioClipGamewin;
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField] private AudioClip gameplayBackgroundClip;
    [SerializeField] private AudioClip bulletsplashClip; //optional can be changed on demand specific to game use

    [Header("Pooling Settings")]
    [Tooltip("Maximum number of pooled SFX sources")]
    [SerializeField] private int maxPooledSources = 10;
    [Tooltip("Extra time before returning source to pool (prevents cut-off)")]
    [SerializeField] private float poolReleaseDelay = 0.1f;
    #endregion

    #region Private Fields
    private readonly Queue<AudioSource> _sfxPool = new();
    private readonly List<AudioSource> _activeSFXSources = new();
    private Coroutine _musicFadeCoroutine;
    private bool _isInitialized;
    #endregion

    #region Properties
    public float MasterVolume 
    { 
        get => masterVolume; 
        set => SetMasterVolume(value); 
    }
    
    public bool IsMusicPlaying => musicSource?.isPlaying == true;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeSingleton();
        SetupAudioSources();
        ApplyAllVolumes();
        _isInitialized = true;
    }

    void Start()
    {
        PlayMenuMusic();
        SetMusicVolume(0.3f);
        // PlayGameplayMusic(crossfadeDuration: 0.5f);
    }

    private void OnDestroy()
    {
        // Clean up pooled sources to prevent memory leaks
        foreach (var source in _sfxPool)
        {
            if (source != null && source.gameObject != null)
                Destroy(source.gameObject);
        }
        _sfxPool.Clear();
        _activeSFXSources.Clear();
        
        if (_musicFadeCoroutine != null)
            StopCoroutine(_musicFadeCoroutine);
    }
    #endregion

    #region Initialization
    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void SetupAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.name = "MusicSource";
        }
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f; // 2D sound

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.name = "SFXSource_Main";
        }
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f; // 2D sound by default
    }
    #endregion

    #region Public API - Music
    /// <summary>Plays a music clip with optional fade-in.</summary>
    public void PlayMusic(AudioClip clip, float fadeDuration = 0f)
    {
        if (clip == null) return;
        
        if (_musicFadeCoroutine != null)
            StopCoroutine(_musicFadeCoroutine);
            
        musicSource.clip = clip;
        
        if (fadeDuration > 0f)
        {
            musicSource.volume = 0f;
            musicSource.Play();
            _musicFadeCoroutine = StartCoroutine(FadeVolume(musicSource, masterVolume * musicVolume, fadeDuration));
        }
        else
        {
            musicSource.volume = masterVolume * musicVolume;
            musicSource.Play();
        }
    }

    /// <summary>Stops music with optional fade-out.</summary>
    public void StopMusic(float fadeDuration = 0f)
    {
        if (!musicSource.isPlaying) return;
        
        if (fadeDuration > 0f)
        {
            if (_musicFadeCoroutine != null)
                StopCoroutine(_musicFadeCoroutine);
            _musicFadeCoroutine = StartCoroutine(FadeOutAndStop(musicSource, fadeDuration));
        }
        else
        {
            musicSource.Stop();
        }
    }

    /// <summary>Pauses or resumes music playback.</summary>
    public void ToggleMusicPause(bool pause)
    {
        if (musicSource == null) return;
        musicSource.Pause();
        if (!pause) musicSource.UnPause();
    }
    #endregion

    #region Public API - SFX
    /// <summary>Plays a 2D sound effect using pooling.</summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null || !_isInitialized) return;
        
        var source = GetPooledSource();
        if (source == null) return;
        
        source.clip = clip;
        source.volume = Mathf.Clamp01(masterVolume * sfxVolume * volumeScale);
        source.pitch = pitch;
        source.Play();
        
        _activeSFXSources.Add(source);
        StartCoroutine(ReleaseToPoolAfterDelay(source, clip.length));
    }

    /// <summary>Plays a 3D sound effect at a world position.</summary>
    public void PlaySFX3D(AudioClip clip, Vector3 position, float volumeScale = 1f, float minDistance = 1f, float maxDistance = 500f)
    {
        if (clip == null || !_isInitialized) return;
        
        var source = GetPooledSource();
        if (source == null) return;
        
        source.clip = clip;
        source.transform.position = position;
        source.spatialBlend = 1f; // 3D sound
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = Mathf.Clamp01(masterVolume * sfxVolume * volumeScale);
        source.Play();
        
        _activeSFXSources.Add(source);
        StartCoroutine(ReleaseToPoolAfterDelay(source, clip.length));
    }

    /// <summary>Stops all currently playing SFX immediately.</summary>
    public void StopAllSFX()
    {
        foreach (var source in _activeSFXSources)
        {
            if (source != null)
            {
                source.Stop();
                ReturnToPool(source);
            }
        }
        _activeSFXSources.Clear();
    }
    #endregion

    #region Volume Control
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyAllVolumes();
        // Optional: Save to PlayerPrefs
        // PlayerPrefs.SetFloat("MasterVolume", masterVolume);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyMusicVolume();
        // PlayerPrefs.SetFloat("MusicVolume", musicVolume);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplySFXVolumeToActive();
        // PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
    }

    /// <summary>Fades all volume types to target values over time.</summary>
    public void FadeAllVolumes(float targetMaster, float targetMusic, float targetSFX, float duration)
    {
        StartCoroutine(FadeVolumesCoroutine(targetMaster, targetMusic, targetSFX, duration));
    }
    #endregion

    #region Convenience Methods (Decouple from game logic)
    /// <summary>Play UI button click sound.</summary>
    public void PlayButtonSFX() => PlaySFX(audioClipButton);
    
    /// <summary>Play planet collection sound.</summary>
    public void PlayGameWinSFX() => PlaySFX(audioClipGamewin);
    
    /// <summary>Play game over sound.</summary>
    public void PlayGameoverSFX() => PlaySFX(audioClipGameover);
    public void PlayBulletSplashSFX() => PlaySFX(bulletsplashClip);
    
    /// <summary>Start main menu background music.</summary>
    public void PlayMenuMusic() => PlayMusic(backgroundMusicClip);
    
    /// <summary>Start gameplay background music with crossfade.</summary>
    public void PlayGameplayMusic(float crossfadeDuration = 1f)
    {
        if (IsMusicPlaying)
            StartCoroutine(CrossfadeMusic(gameplayBackgroundClip, crossfadeDuration));
        else
            PlayMusic(gameplayBackgroundClip);
    }
    #endregion

    #region Pooling System
    private AudioSource GetPooledSource()
    {
        // Try to get from pool
        while (_sfxPool.Count > 0)
        {
            var source = _sfxPool.Dequeue();
            if (source != null && source.gameObject != null)
                return source;
        }
        
        // Create new if under limit
        if (transform.childCount < maxPooledSources)
            return CreateNewPooledSource();
            
        // Fallback: reuse main SFX source (may interrupt)
        Debug.LogWarning("SFX pool exhausted - using fallback source");
        return sfxSource;
    }

    private AudioSource CreateNewPooledSource()
    {
        var obj = new GameObject($"SFX_Pooled_{_sfxPool.Count + _activeSFXSources.Count}");
        obj.transform.SetParent(transform, false);
        
        var source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        
        return source;
    }

    private void ReturnToPool(AudioSource source)
    {
        if (source == null || source == sfxSource || source == musicSource)
            return;
            
        source.clip = null;
        source.spatialBlend = 0f; // Reset to 2D default
        _activeSFXSources.Remove(source);
        
        if (_sfxPool.Count < maxPooledSources)
            _sfxPool.Enqueue(source);
        else
            Destroy(source.gameObject);
    }

    private IEnumerator ReleaseToPoolAfterDelay(AudioSource source, float clipLength)
    {
        // Wait for clip to finish + buffer
        yield return new WaitForSecondsRealtime(clipLength + poolReleaseDelay);
        
        if (source != null && !source.isPlaying)
            ReturnToPool(source);
    }
    #endregion

    #region Volume Application Helpers
    private void ApplyAllVolumes()
    {
        ApplyMusicVolume();
        ApplySFXVolumeToActive();
    }

    private void ApplyMusicVolume()
    {
        if (musicSource != null)
            musicSource.volume = Mathf.Clamp01(masterVolume * musicVolume);
    }

    private void ApplySFXVolumeToActive()
    {
        foreach (var source in _activeSFXSources)
        {
            if (source != null && source.isPlaying)
                source.volume = Mathf.Clamp01(masterVolume * sfxVolume);
        }
    }
    #endregion

    #region Coroutines
    private IEnumerator FadeVolume(AudioSource source, float targetVolume, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }
        
        source.volume = targetVolume;
        _musicFadeCoroutine = null;
    }

    private IEnumerator FadeOutAndStop(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        
        source.Stop();
        source.volume = startVolume; // Reset for next use
        _musicFadeCoroutine = null;
    }

    private IEnumerator FadeVolumesCoroutine(float targetMaster, float targetMusic, float targetSFX, float duration)
    {
        float startMaster = masterVolume;
        float startMusic = musicVolume;
        float startSFX = sfxVolume;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            masterVolume = Mathf.Lerp(startMaster, targetMaster, t);
            musicVolume = Mathf.Lerp(startMusic, targetMusic, t);
            sfxVolume = Mathf.Lerp(startSFX, targetSFX, t);
            
            ApplyAllVolumes();
            yield return null;
        }
        
        // Ensure final values are exact
        masterVolume = targetMaster;
        musicVolume = targetMusic;
        sfxVolume = targetSFX;
        ApplyAllVolumes();
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip, float duration)
    {
        // Fade out current
        yield return StartCoroutine(FadeVolume(musicSource, 0f, duration));
        musicSource.Stop();
        
        // Play new and fade in
        musicSource.clip = newClip;
        musicSource.volume = 0f;
        musicSource.Play();
        yield return StartCoroutine(FadeVolume(musicSource, masterVolume * musicVolume, duration));
    }
    #endregion

    #region Optional: Persistence Helpers
    /// <summary>Load saved volume settings from PlayerPrefs.</summary>
    public void LoadSavedVolumes()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
        ApplyAllVolumes();
    }

    /// <summary>Save current volume settings to PlayerPrefs.</summary>
    public void SaveVolumes()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.Save();
    }
    #endregion
}