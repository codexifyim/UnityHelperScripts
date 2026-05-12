using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource musicSource;        // For background music (looping)
    public AudioSource sfxSource;          // For short sound effects (non-looping)

    [Header("Volume Controls")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    // Optional: Cache for SFX pooling (prevents garbage collection)
    private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
    private const int maxPooledSources = 10;

    [SerializeField] private AudioClip audioClipButton;
    [SerializeField] private AudioClip audioClipGameover, audioClipPoint;
    [SerializeField] private AudioClip backgroundMusicClip, gameplayBackgroundClip;
    [SerializeField] private AudioClip touchPlanetClip;


    void Awake()
    {
        // Singleton + DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Ensure sources exist
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = true;
        // musicSource.volume = 0.4f;
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        ApplyVolume();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource.isPlaying)
            musicSource.Stop();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        // Use pooled AudioSource or create new one
        AudioSource source = GetPooledSFXSource();
        source.clip = clip;
        source.volume = sfxVolume * masterVolume;
        source.Play();

        // Auto-return to pool after clip finishes
        StartCoroutine(ReleaseAfterDelay(source, clip.length));
    }

    // Optional: Play SFX at position (for 3D)
    public void PlaySFX(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, sfxVolume * masterVolume);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyVolume();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        // Note: active SFX won't change, but new ones will
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        musicSource.volume = musicVolume * masterVolume;
    }

    // --- SFX Pooling ---
    private AudioSource GetPooledSFXSource()
    {
        if (sfxPool.Count > 0)
        {
            AudioSource source = sfxPool.Dequeue();
            if (source == null) // in case it was destroyed
                return CreateNewSFXSource();
            return source;
        }

        if (transform.childCount < maxPooledSources)
            return CreateNewSFXSource();

        // Fallback: use main sfxSource (may cut off previous sound)
        return sfxSource;
    }

    private AudioSource CreateNewSFXSource()
    {
        GameObject obj = new GameObject("SFX_Source");
        obj.transform.SetParent(transform);
        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        return source;
    }

    private System.Collections.IEnumerator ReleaseAfterDelay(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay + 0.1f);
        if (source != sfxSource && source != null)
        {
            source.clip = null;
            sfxPool.Enqueue(source);
        }
    }

    public void ButtonClicked()
    {
        PlaySFX(audioClipButton);
    }
    public void BackgroundMusic()
    {
        PlayMusic(backgroundMusicClip);
    }

    public void GameplayBackgroundMusic()
    {
        PlayMusic(gameplayBackgroundClip);
    }
    public void GameOverMusic()
    {
        PlaySFX(audioClipGameover);
    }
    public void PlanetCollected()
    {
        PlaySFX(touchPlanetClip);

    }
}