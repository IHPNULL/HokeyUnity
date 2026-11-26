using UnityEngine;

// Every sound and music track the game plays, in one asset (Resources/Audio/SoundBank). The clips
// come from free Unity Asset Store packs; anything left empty falls back to a synthesized sound.
[CreateAssetMenu(menuName = "Hockey/Sound Bank", fileName = "SoundBank")]
public class SoundBank : ScriptableObject
{
    [Header("Music")]
    public AudioClip menuMusic;
    public AudioClip matchMusic;

    [Header("Match")]
    public AudioClip goalHorn;
    public AudioClip periodBuzzer;
    public AudioClip countdownBeep;
    public AudioClip countdownGo;
    public AudioClip kick;

    [Header("Impacts")]
    public AudioClip[] robotCrash;    // robot on robot
    public AudioClip[] robotBoards;   // robot on boards
    public AudioClip[] puckHit;       // the puck on anything

    [Header("Crowd")]
    public AudioClip crowdAmbience;
    public AudioClip crowdCheer;

    static SoundBank cached; static bool looked;
    public static SoundBank Get()
    {
        if (!looked || cached == null) { cached = Resources.Load<SoundBank>("Audio/SoundBank"); looked = true; }
        return cached;
    }
    public static AudioClip Pick(AudioClip[] set) => set != null && set.Length > 0 ? set[Random.Range(0, set.Length)] : null;
}
