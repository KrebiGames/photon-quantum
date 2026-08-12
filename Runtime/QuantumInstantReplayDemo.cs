namespace Quantum {
  using UnityEngine;
  using static QuantumUnityExtensions;

  /// <summary>
  /// A script to demonstrate the instant replay feature of Quantum.
  /// Add this script to a GameObject in your scene to enable the instant replays.
  /// Press the "Start" button during runtime to start a replay.
  /// Uses the <see cref="QuantumInstantReplay"/> class. 
  /// </summary>
  public class QuantumInstantReplayDemo : QuantumMonoBehaviour {
    /// <summary>
    /// The playback speed of the replay. Default is 1.0f.
    /// </summary>
    [InlineHelp]
    public float PlaybackSpeed = 1.0f;
    /// <summary>
    /// The length of the replay in seconds. Default is 2.0f.
    /// </summary>
    [InlineHelp]
    public float ReplayLengthSec = 2.0f;
    /// <summary>
    /// If set to true, displays a replay label on the screen during the replay.
    /// </summary>
    [InlineHelp]
    public bool ShowReplayLabel = true;
    /// <summary>
    /// If set to true, displays a fading effect when starting and stopping the replay.
    /// </summary>
    [InlineHelp]
    public bool ShowFadingEffect = true;
    /// <summary>
    /// Read-only flag to indicate if the replay is running.
    /// </summary>
    [InlineHelp, ReadOnly] public bool IsReplayRunning;
    /// <summary>
    /// Read-only flag to indicate the start button can be pressed.
    /// </summary>
    [InlineHelp, ReadOnly] public bool Button_StartInstantReplay;
    /// <summary>
    /// Read-only flag to indicate the stop button can be pressed.
    /// </summary>
    [InlineHelp, ReadOnly] public bool Button_StopInstantReplay;
    /// <summary>
    /// Set the rewind mode to loop the replay or seek to a desired frame.
    /// </summary>
    [InlineHelp]
    public QuantumInstantReplaySeekMode RewindMode = QuantumInstantReplaySeekMode.Disabled;
    /// <summary>
    /// Loops the replay. Only available when <see cref="RewindMode"/> is not disabled.
    /// </summary>
    [InlineHelp, DrawIf(nameof(RewindMode), Mode = DrawIfMode.Hide)] 
    public bool EnableLoop = false;
    /// <summary>
    /// The replay normalized time. This value is between 0 and 1.
    /// Use the slider to jump to a desired time in the replay.
    /// </summary>
    [InlineHelp, Range(0, 1)]
    public float NormalizedTime;

    private float previousNormalizedTime;

    QuantumInstantReplay _instantReplay;
    bool _isInstantReplayRunning;
    bool _isFading;
    float _fadingAlpha = 1.0f;
    Texture2D _fadingTexture;
    float _fadingTime;

    #region Unity Callbacks

    /// <summary>
    /// Unity Awake event, subscribe to the game destroyed event and clean up stopped replays.
    /// </summary>
    public void Awake() {
      QuantumCallback.Subscribe(this, (CallbackGameDestroyed c) => {
        if (_instantReplay == null)
          return;

        if (c.Game == _instantReplay.LiveGame) {
          // main game was shut down, shut down replay
          StopReplayIfRunning();
          // Dispose replay runner and release memory
          _instantReplay?.Dispose();
          _instantReplay = null;
        } else if (c.Game == _instantReplay.ReplayGame) {
          // Replay runner is shutdown, then we expect the instant replay is also disposed there.
          StopReplayIfRunning();
          _instantReplay = null;
        }
      });
    }

    /// <summary>
    /// Unity Update event. Toggle recording snapshots and update the replay.
    /// Update the debug buttons and trigger seeking the replay.
    /// </summary>
    public void Update() {
      if (QuantumRunner.Default != null) {
        // Tell the game to start capturing snapshots. This can be called at any point in the game.
        QuantumRunner.Default.Game.StartRecordingInstantReplaySnapshots();
      }

      if (_isInstantReplayRunning) {
        if (_instantReplay.CanSeek) {
          if (previousNormalizedTime != NormalizedTime) {
            _instantReplay.SeekNormalizedTime(NormalizedTime);
          }
        }

        if (_instantReplay.Update(Time.unscaledDeltaTime * PlaybackSpeed)) {
          previousNormalizedTime = NormalizedTime = _instantReplay.NormalizedTime;
        } else {
          StopReplayIfRunning();
        }
      }

      Button_StartInstantReplay = !_isInstantReplayRunning && QuantumRunner.Default != null;
      Button_StopInstantReplay = _isInstantReplayRunning;
      IsReplayRunning = _isInstantReplayRunning;
    }

    private void StopReplayIfRunning() {
      if (_isInstantReplayRunning) {
        _isInstantReplayRunning = false;
        OnReplayStopped();
      }
    }

    /// <summary>
    /// Unity OnDisabled event, disposes the instant replay data structures.
    /// </summary>
    public void OnDisable() {
      StopReplayIfRunning();
      _instantReplay?.Dispose();
      _instantReplay = null;
    }

    /// <summary>
    /// Unity OnDestroy event, destroys the fading texture.
    /// </summary>
    public void OnDestroy() {
      if (_fadingTexture != null)
        Destroy(_fadingTexture);
      _fadingTexture = null;
    }

    /// <summary>
    /// Unity OnGUI event, displays the replay label and the replay slider.
    /// </summary>
    public void OnGUI() {
      if (ShowReplayLabel && _isInstantReplayRunning) {
        GUI.contentColor = Color.red;
        GUI.Label(new Rect(10, 30, 200, 100), "INSTANT REPLAY");

        bool guiEnabled = GUI.enabled;
        try {
          GUI.enabled = _instantReplay.CanSeek;
          var frameNumber = _instantReplay.ReplayGame.Frames.Verified.Number;
          var seekFrameNumber = (int)GUI.HorizontalSlider(new Rect(10, 50, 150, 100), frameNumber, _instantReplay.StartFrame, _instantReplay.EndFrame);
          if (_instantReplay.CanSeek && frameNumber != seekFrameNumber) {
            _instantReplay.SeekFrame(seekFrameNumber);
          }
        } finally {
          GUI.enabled = guiEnabled;
        }
      }

      if (_isFading) {
        _fadingTime += Time.deltaTime;
        _fadingAlpha = Mathf.Lerp(1.0f, 0.0f, _fadingTime);

        if (_fadingTexture == null)
          _fadingTexture = new Texture2D(1, 1);

        _fadingTexture.SetPixel(0, 0, new Color(0, 0, 0, _fadingAlpha));
        _fadingTexture.Apply();

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _fadingTexture);

        _isFading = _fadingAlpha > 0;
      }
    }

    #endregion

    #region Instant Replay Callbacks

    void OnReplayStarted(QuantumGame game) {
      Debug.LogFormat("### Starting Quantum instant replay at frame {0} ###", game.Frames.Predicted.Number);

      // FindObjectOfType is super slow, but it serves the demo purpose here.
      var entityViewUpdater = FindAnyObjectByType<QuantumEntityViewUpdater>();
      if (entityViewUpdater != null) {
        entityViewUpdater.SetCurrentGame(game);
        entityViewUpdater.TeleportAllEntities();
      }

      StartFading();
    }

    void OnReplayStopped() {
      Debug.LogFormat("### Stopping Quantum instant replay and resuming the live game ###");
      
      QuantumRunnerRegistry.Global.RemoveRunner(_instantReplay.ReplayRunner);

      var entityViewUpdater = FindAnyObjectByType<QuantumEntityViewUpdater>();
      if (entityViewUpdater != null) {
        entityViewUpdater.SetCurrentGame(QuantumRunner.Default.Game);
        entityViewUpdater.TeleportAllEntities();
      }

      StartFading();
    }

    void StartFading() {
      if (ShowFadingEffect) {
        _isFading = true;
        _fadingAlpha = 1.0f;
        _fadingTime = 0.0f;
      }
    }

    #endregion

    #region Editor Button

    /// <summary>
    /// Is called from the inspector to start the instant replay.
    /// </summary>
    public void Editor_StartInstantReplay() {
      if (_isInstantReplayRunning == false && QuantumRunner.Default) {
        _instantReplay ??= new QuantumInstantReplay();
        _instantReplay.Run(QuantumRunner.Default.Game, ReplayLengthSec, RewindMode, EnableLoop);
        _isInstantReplayRunning = true;
        OnReplayStarted(_instantReplay.ReplayGame);
      }
    }

    /// <summary>
    /// Is called from the inspector to stop the instant replay.
    /// </summary>
    public void Editor_StopInstantReplay() {
      if (_isInstantReplayRunning) {
        StopReplayIfRunning();
      }
    }

    #endregion
  }
}