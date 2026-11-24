using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Range(0f, 1f)] public float musicVolume = 0.45f;
    [Range(0f, 1f)] public float sfxVolume = 0.85f;

    private AudioClip pickup, place, error, hint, win, music;
    private AudioSource sfxSource;
    private AudioSource musicSource;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        pickup = Resources.Load<AudioClip>("Audio/pickup");
        place = Resources.Load<AudioClip>("Audio/place");
        error = Resources.Load<AudioClip>("Audio/error");
        hint = Resources.Load<AudioClip>("Audio/hint");
        win = Resources.Load<AudioClip>("Audio/win");
        music = Resources.Load<AudioClip>("Audio/music");

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.loop = true;
        if (music != null) musicSource.clip = music;
        musicSource.volume = musicVolume;
    }

    public void PlaySfx(string name)
    {
        AudioClip clip = name switch
        {
            "pickup" => pickup,
            "place" => place,
            "error" => error,
            "hint" => hint,
            "win" => win,
            _ => null
        };
        if (clip != null) sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void PlayMusic()
    {
        if (musicSource != null && !musicSource.isPlaying && music != null)
            musicSource.Play();
    }

    public void PauseMusic()
    {
        if (musicSource != null && musicSource.isPlaying) musicSource.Pause();
    }

    public void ResumeMusic()
    {
        if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
            musicSource.UnPause();
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
    }
}
