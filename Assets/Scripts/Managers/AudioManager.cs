using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Centralized audio manager. Handles music, SFX, and haptics.
/// Reads volume/vibration settings from SaveSystem.
/// 
/// Attach AudioClips in the inspector for each game event.
/// For haptics, uses Unity's Handheld.Vibrate or custom durations.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSfxSource; // Separate source for UI (not affected by slow-mo, etc.)
    
    [Header("Music")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameplayMusic;
    [SerializeField] private float musicFadeDuration = 1f;
    
    [Header("Gameplay SFX")]
    [SerializeField] private AudioClip floorPickUp;
    [SerializeField] private AudioClip floorPlace;
    [SerializeField] private AudioClip stackComplete;
    [SerializeField] private AudioClip levelComplete;
    [SerializeField] private AudioClip moveFailed;
    [SerializeField] private AudioClip undoSound;
    [SerializeField] private AudioClip hintSound;
    
    [Header("UI SFX")]
    [SerializeField] private AudioClip buttonClick;
    [SerializeField] private AudioClip buttonBack;
    [SerializeField] private AudioClip popupOpen;
    [SerializeField] private AudioClip popupClose;
    [SerializeField] private AudioClip coinEarn;
    [SerializeField] private AudioClip coinSpend;
    [SerializeField] private AudioClip starEarn;
    [SerializeField] private AudioClip achievementSound;
    
    [Header("Haptic Durations (ms)")]
    [SerializeField] private long lightHaptic = 10;
    [SerializeField] private long mediumHaptic = 25;
    [SerializeField] private long heavyHaptic = 50;
    
    // Pitch variation for natural feel
    [Header("Variation")]
    [SerializeField] private float pitchVariation = 0.05f;
    
    private Coroutine musicFadeCoroutine;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        ApplySettings();
    }
    
    // ========================================
    // SETTINGS
    // ========================================
    
    /// <summary>
    /// Apply volume settings from save data
    /// </summary>
    public void ApplySettings()
    {
        AudioListener.volume = SaveSystem.Data.masterVolume;

        if (musicSource != null)
            musicSource.volume = SaveSystem.Data.musicVolume;
        if (sfxSource != null)
            sfxSource.volume = SaveSystem.Data.sfxVolume;
        if (uiSfxSource != null)
            uiSfxSource.volume = SaveSystem.Data.sfxVolume;
    }

    public void SetMasterVolume(float volume)
    {
        SaveSystem.Data.masterVolume = volume;
        AudioListener.volume = volume;
        SaveSystem.Save();
    }
    
    public void SetMusicVolume(float volume)
    {
        SaveSystem.Data.musicVolume = volume;
        if (musicSource != null)
            musicSource.volume = volume;
        SaveSystem.Save();
    }
    
    public void SetSfxVolume(float volume)
    {
        SaveSystem.Data.sfxVolume = volume;
        if (sfxSource != null)
            sfxSource.volume = volume;
        if (uiSfxSource != null)
            uiSfxSource.volume = volume;
        SaveSystem.Save();
    }
    
    public void SetVibration(bool enabled)
    {
        SaveSystem.Data.vibrationEnabled = enabled;
        SaveSystem.Save();
    }
    
    // ========================================
    // MUSIC
    // ========================================
    
    public void PlayMenuMusic()
    {
        CrossfadeMusic(menuMusic);
    }
    
    public void PlayGameplayMusic()
    {
        CrossfadeMusic(gameplayMusic);
    }
    
    public void StopMusic()
    {
        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);
        musicFadeCoroutine = StartCoroutine(FadeOutMusic());
    }
    
    private void CrossfadeMusic(AudioClip newClip)
    {
        if (newClip == null) return;
        if (musicSource != null && musicSource.clip == newClip && musicSource.isPlaying) return;
        
        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);
        musicFadeCoroutine = StartCoroutine(CrossfadeMusicRoutine(newClip));
    }
    
    private IEnumerator CrossfadeMusicRoutine(AudioClip newClip)
    {
        float targetVolume = SaveSystem.Data.musicVolume;
        
        // Fade out current
        if (musicSource != null && musicSource.isPlaying)
        {
            float startVol = musicSource.volume;
            float elapsed = 0;
            while (elapsed < musicFadeDuration * 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVol, 0, elapsed / (musicFadeDuration * 0.5f));
                yield return null;
            }
        }
        
        // Switch and fade in
        if (musicSource != null)
        {
            musicSource.clip = newClip;
            musicSource.loop = true;
            musicSource.Play();
            
            float elapsed = 0;
            while (elapsed < musicFadeDuration * 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(0, targetVolume, elapsed / (musicFadeDuration * 0.5f));
                yield return null;
            }
            musicSource.volume = targetVolume;
        }
    }
    
    private IEnumerator FadeOutMusic()
    {
        if (musicSource == null || !musicSource.isPlaying) yield break;
        
        float startVol = musicSource.volume;
        float elapsed = 0;
        while (elapsed < musicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVol, 0, elapsed / musicFadeDuration);
            yield return null;
        }
        musicSource.Stop();
    }
    
    // ========================================
    // SFX
    // ========================================
    
    private void PlaySFX(AudioClip clip, bool useVariation = true)
    {
        if (clip == null || sfxSource == null) return;
        
        if (useVariation)
            sfxSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        else
            sfxSource.pitch = 1f;
        
        sfxSource.PlayOneShot(clip);
    }
    
    private void PlayUISFX(AudioClip clip)
    {
        if (clip == null || uiSfxSource == null) return;
        uiSfxSource.pitch = 1f;
        uiSfxSource.PlayOneShot(clip);
    }
    
    // --- Gameplay sounds ---
    
    public void PlayFloorPickUp()
    {
        PlaySFX(floorPickUp);
        HapticLight();
    }
    
    public void PlayFloorPlace()
    {
        PlaySFX(floorPlace);
        HapticMedium();
    }
    
    public void PlayStackComplete()
    {
        PlaySFX(stackComplete, false);
        HapticHeavy();
    }
    
    public void PlayLevelComplete()
    {
        PlaySFX(levelComplete, false);
        HapticHeavy();
    }
    
    public void PlayMoveFailed()
    {
        PlaySFX(moveFailed, false);
        HapticFailure();
    }
    
    public void PlayUndo()
    {
        PlaySFX(undoSound);
    }
    
    public void PlayHint()
    {
        PlaySFX(hintSound);
    }
    
    // --- UI sounds ---
    
    public void PlayButtonClick()
    {
        PlayUISFX(buttonClick);
        HapticLight();
    }
    
    public void PlayButtonBack()
    {
        PlayUISFX(buttonBack);
    }
    
    public void PlayPopupOpen()
    {
        PlayUISFX(popupOpen);
    }
    
    public void PlayPopupClose()
    {
        PlayUISFX(popupClose);
    }
    
    public void PlayCoinEarn()
    {
        PlayUISFX(coinEarn);
        HapticLight();
    }
    
    public void PlayCoinSpend()
    {
        PlayUISFX(coinSpend);
        HapticMedium();
    }
    
    public void PlayStarEarn()
    {
        PlayUISFX(starEarn);
        HapticMedium();
    }
    
    public void PlayAchievement()
    {
        PlayUISFX(achievementSound);
        HapticMedium();
    }
    
    // ========================================
    // HAPTICS
    // ========================================
    
    public void HapticLight()
    {
        if (!SaveSystem.Data.vibrationEnabled) return;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(lightHaptic);
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS uses UIImpactFeedbackGenerator — requires native plugin
        // Fallback to simple vibrate
        Handheld.Vibrate();
#endif
    }
    
    public void HapticMedium()
    {
        if (!SaveSystem.Data.vibrationEnabled) return;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(mediumHaptic);
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
    
    public void HapticHeavy()
    {
        if (!SaveSystem.Data.vibrationEnabled) return;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(heavyHaptic);
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    public void HapticFailure()
    {
        if (!SaveSystem.Data.vibrationEnabled) return;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(FailureHapticRoutine());
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator FailureHapticRoutine()
    {
        VibrateAndroid(20); // First light tap
        yield return new WaitForSecondsRealtime(0.08f);
        VibrateAndroid(50); // Second stronger tap
    }
#endif
    
#if UNITY_ANDROID && !UNITY_EDITOR
    private void VibrateAndroid(long milliseconds)
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                
                if (vibrator != null)
                {
                    // API 26+ (Android O) uses VibrationEffect
                    if (GetAndroidApiLevel() >= 26)
                    {
                        var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
                        var effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, -1);
                        vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AudioManager] Haptic failed: {e.Message}");
        }
    }
    
    private int GetAndroidApiLevel()
    {
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            return version.GetStatic<int>("SDK_INT");
        }
    }
#endif
}
