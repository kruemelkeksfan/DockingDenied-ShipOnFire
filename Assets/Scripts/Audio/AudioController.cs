using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AudioController : MonoBehaviour, IListener
{
	private class AudioData
	{
		public double loopTime;
		public double nextPlaytime;
		public int playCounter;
	}

	private static AudioController instance = null;

	[SerializeField] private AudioClip clickAudio = null;
	[SerializeField] private AudioClip[] music = { };
	[SerializeField] private AudioSource musicSource = null;
	[SerializeField] private AudioSource sfxSource = null;
	[SerializeField] private float audioUpdateInterval = 0.1f;
	[SerializeField] private float maximumPauseDuration = 30.0f;
	[SerializeField] private float audioLoopOverlapFactor = 0.5f;
	[SerializeField] private Slider musicSlider = null;
	[SerializeField] private InputField musicInputField = null;
	[SerializeField] private Slider sfxSlider = null;
	[SerializeField] private InputField sfxInputField = null;
	[SerializeField] private string creditUrl = "https://freepd.com/artists.php";
	private TimeController timeController = null;
	private SpacecraftManager spacecraftManager = null;
	private HashSet<AudioClip> oneShotAudios = null;
	private Dictionary<AudioClip, AudioData> loopedAudios = null;
	private GameObject localPlayerMainObject = null;

	public static AudioController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		oneShotAudios = new HashSet<AudioClip>();
		loopedAudios = new Dictionary<AudioClip, AudioData>();

		float volume = musicSource.volume;
		musicSlider.value = volume;
		musicInputField.text = volume.ToString("F2");
		volume = sfxSource.volume;
		sfxSlider.value = volume;
		sfxInputField.text = volume.ToString("F2");

		timeController = TimeController.GetInstance();
		timeController.StartCoroutine(AudioUpdate(), true);

		spacecraftManager = SpacecraftManager.GetInstance();
		spacecraftManager.AddSpacecraftChangeListener(this);
		Notify();
	}

	public void Notify()
	{
		localPlayerMainObject = spacecraftManager.GetLocalPlayerMainSpacecraft().gameObject;

		// Disable all looped Audios on Ship Change
		loopedAudios.Clear();

		// TODO: On Ship Change, start all necessary new Audios (running Thrusters or active Docking Ports etc. on new Ship)
	}

	private IEnumerator<float> AudioUpdate()
	{
		int nextTitle = Random.Range(0, music.Length - 1);
		double pauseUntil = 0.0;
		while(true)
		{
			// Get current Time
			double time = Time.realtimeSinceStartupAsDouble;

			// Music
			if(time > pauseUntil)
			{
				// Do not play the same Title twice in a Row
				int currentTitle = nextTitle;
				while(nextTitle == currentTitle)
				{
					nextTitle = Random.Range(0, music.Length - 1);
				}

				// Random Pause after Title
				pauseUntil = time + music[currentTitle].length + Random.Range(0.0f, maximumPauseDuration);

				// Play Title
				musicSource.PlayOneShot(music[currentTitle]);
			}

			// SFX
			oneShotAudios.Clear();
			foreach(KeyValuePair<AudioClip, AudioData> audioEntry in loopedAudios)
			{
				if(audioEntry.Value.nextPlaytime <= time)
				{
					sfxSource.PlayOneShot(audioEntry.Key);

					audioEntry.Value.nextPlaytime = time + audioEntry.Value.loopTime;
				}
			}

			// Wait
			yield return audioUpdateInterval;
		}
	}

	public void PlayAudio(AudioClip audio, GameObject triggeringObject)
	{
		if(triggeringObject == null || triggeringObject == localPlayerMainObject)
		{
			// Play every Audio at most once per Audio Frame
			if(!oneShotAudios.Contains(audio))
			{
				sfxSource.PlayOneShot(audio);
				oneShotAudios.Add(audio);
			}
		}
	}

	public void LoopAudioStart(AudioClip audio, GameObject triggeringObject)
	{
		if(triggeringObject == null || triggeringObject == localPlayerMainObject)
		{
			AudioData audioData;
			if(loopedAudios.TryGetValue(audio, out audioData))
			{
				++audioData.playCounter;
			}
			else
			{
				audioData = new AudioData();
				audioData.nextPlaytime = Time.realtimeSinceStartupAsDouble;
				audioData.loopTime = audio.length * audioLoopOverlapFactor;
				audioData.playCounter = 1;
				loopedAudios.Add(audio, audioData);
			}
		}
	}

	public void LoopAudioStop(AudioClip audio, GameObject triggeringObject)
	{
		if(triggeringObject == null || triggeringObject == localPlayerMainObject)
		{
			AudioData audioData;

			try
			{
				audioData = loopedAudios[audio];
			}
			catch(KeyNotFoundException)
			{
				Debug.LogWarning(audio + " has already been stopped when LoopAudioStop() was called in AudioController!");
				return;
			}

			--audioData.playCounter;
			if(audioData.playCounter <= 0)
			{
				loopedAudios.Remove(audio);
			}
		}
	}

	public void PlayClickAudio()
	{
		PlayAudio(clickAudio, null);
	}

	public void MusicSliderChanged()
	{
		float volume = musicSlider.value;
		musicInputField.text = volume.ToString("F2");
		musicSource.volume = volume;
	}

	public void MusicInputFieldChanged()
	{
		float volume = Mathf.Clamp01(float.Parse(musicInputField.text));
		musicSlider.value = volume;
		musicInputField.text = volume.ToString("F2");
		musicSource.volume = volume;
	}

	public void SFXSliderChanged()
	{
		float volume = sfxSlider.value;
		sfxInputField.text = volume.ToString("F2");
		sfxSource.volume = volume;
	}

	public void SFXInputFieldChanged()
	{
		float volume = Mathf.Clamp01(float.Parse(sfxInputField.text));
		sfxSlider.value = volume;
		sfxInputField.text = volume.ToString("F2");
		sfxSource.volume = volume;
	}

	public void OpenCreditLink()
	{
		Application.OpenURL(creditUrl);
	}
}
