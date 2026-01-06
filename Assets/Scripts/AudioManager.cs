using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public AudioMixer audioMixer;  // Assign MainAudioMixer in Inspector
    public Slider masterSlider, musicSlider, sfxSlider;  // Assign sliders in Inspector

    private const string MasterKey = "Master";
    private const string MusicKey = "Music";
    private const string SFXKey = "FX";

    void Start()
    {
        // Load saved volumes or set defaults
        masterSlider.value = PlayerPrefs.GetFloat(MasterKey, 1f);
        musicSlider.value = PlayerPrefs.GetFloat(MusicKey, 1f);
        sfxSlider.value = PlayerPrefs.GetFloat(SFXKey, 1f);

        // Set initial volumes
        SetMasterVolume(masterSlider.value);
        SetMusicVolume(musicSlider.value);
        SetSFXVolume(sfxSlider.value);

        // Add listeners to sliders
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    public void SetMasterVolume(float volume)
    {
        SetVolume("Master", volume);
        PlayerPrefs.SetFloat(MasterKey, volume);
    }

    public void SetMusicVolume(float volume)
    {
        SetVolume("Music", volume);
        PlayerPrefs.SetFloat(MusicKey, volume);
    }

    public void SetSFXVolume(float volume)
    {
        SetVolume("FX", volume);
        PlayerPrefs.SetFloat(SFXKey, volume);
    }

    private void SetVolume(string parameterName, float volume)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioMixer is not assigned!");
            return;
        }

        // Convert linear volume to dB
        float dB = volume > 0 ? Mathf.Log10(volume) * 20 : -80f;  // -80f for mute

        // Try to set the volume; log if parameter doesn't exist
        if (!audioMixer.SetFloat(parameterName, dB))
        {
            Debug.LogWarning($"Exposed parameter '{parameterName}' does not exist in AudioMixer. Check AudioMixer setup.");
        }
    }

    void OnDestroy()
    {
        PlayerPrefs.Save();  // Save on exit
    }
}
