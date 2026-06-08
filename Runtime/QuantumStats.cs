namespace Quantum {
  using Photon.Client;
  using Quantum.Core;
  using System;
  using System.Diagnostics;
  using System.Globalization;
  using UnityEngine;
  using UnityEngine.EventSystems;
  using UnityEngine.UI;
  using static QuantumUnityExtensions;

  /// <summary>
  /// Measures and display basic Quantum statistics on a UI element.
  /// </summary>
  public class QuantumStats : QuantumMonoBehaviour {
    /// <summary>
    /// Current verified frame.
    /// </summary>
    public Text FrameVerified;
    /// <summary>
    /// Current predicted frame.
    /// </summary>
    public Text FramePredicted;
    /// <summary>
    /// The number of frames currently predicted into the future.
    /// </summary>
    public Text Predicted;
    /// <summary>
    /// Verified frames per Unity update.
    /// </summary>
    public Text VerifiedFrames;
    /// <summary>
    /// Predicted frames per Unity update.
    /// </summary>
    public Text PredictedFrames;
    /// <summary>
    /// The last simulation time.
    /// </summary>
    public Text SimulateTime;
    /// <summary>
    /// The state of the simulation.
    /// </summary>
    public Text SimulationState;
    /// <summary>
    /// The network ping measured by the simulation.
    /// </summary>
    public Text NetworkPing;
    /// <summary>
    /// The connected region.
    /// </summary>
    public Text Region;
    /// <summary>
    /// The bytes received per second.
    /// </summary>
    public Text NetworkIn;
    /// <summary>
    /// The bytes send per second.
    /// </summary>
    public Text NetworkOut;
    /// <summary>
    /// The current input offset.
    /// </summary>
    public Text InputOffset;
    /// <summary>
    /// Active state.
    /// </summary>
    public GameObject ToggleOn;
    /// <summary>
    /// Inactive state.
    /// </summary>
    public GameObject ToggleOff;
    /// <summary>
    /// Start the game with an open stats window.
    /// </summary>
    public Boolean StartEnabled = true;
    /// <summary>
    /// Use only the last second to measure <see cref="NetworkOut"/> and <see cref="NetworkOut"/> instead of the total time.
    /// </summary>
    public Boolean UseCurrentBandwidth = true;
    /// <summary>
    /// Shows frame and ping information on the toggle button.
    /// </summary>
    public Boolean ShowCompactStats = true;
    /// <summary>
    /// The text field set when <see cref="ShowCompactStats"/> is <see langword="true"/>.
    /// </summary>
    public Text CompactStatsText;
    /// <summary>
    /// Interval in seconds between text-field refreshes. Sampling into the smoothed buffers continues every
    /// frame; only the UI assignments are throttled to keep allocations and uGUI mesh rebuilds down.
    /// </summary>
    public float UiRefreshIntervalSec = 0.1f;

    const float SmoothingTimeSec = 1f;

    double _lastTime = 0;
    int _compactLastFrameSeen = 0;
    int _verifiedDeltaPrevFrame = 0;
    int _lastShownFrameVerified = -1;
    int _lastShownFramePredicted = -1;
    float _nextUiUpdateTime;
    SmoothedValue _prediction;
    SmoothedValue _verifiedFrames;
    SmoothedValue _predictedFrames;
    SmoothedValue _simulateTime;
    Stopwatch _networkTimer;
    TrafficStatsSnapshot _snapshotDelta;

    /// <summary>
    /// Resets the bandwidth stats measurement.
    /// </summary>
    public void ResetNetworkStats() {
      _networkTimer = null;
      _snapshotDelta = null;
      _lastTime = 0;
    }

    void Start() {
      // create event system if none exists in the scene
      var eventSystem = FindAnyObjectByType<EventSystem>();
      if (eventSystem == null) {
        gameObject.AddComponent<EventSystem>();
        gameObject.AddComponent<QuantumUnityInputSystemWithLegacyFallback>();
      }

      SetState(StartEnabled);
    }

    void LateUpdate() {
      if (QuantumRunner.Default && ToggleOff.activeSelf) {
        if (QuantumRunner.Default.IsRunning) {
          if (ShowCompactStats) {
            var currentFrame = QuantumRunner.Default.Game.Session.FrameVerified.Number;
            if (_compactLastFrameSeen != currentFrame) {
              _compactLastFrameSeen = currentFrame;
              var ping = QuantumRunner.Default.Game.Session.Stats.Ping;
              CompactStatsText.text =
                QuantumRunner.Default.Game.Session.IsOnline
                ? $"Frame {FormatFrame(currentFrame, QuantumRunner.Default.Game.Session.IsStalling),5}  Ping {FormatPing(ping),3}"
                : $"Frame {FormatFrame(currentFrame, QuantumRunner.Default.Game.Session.IsStalling),5}   (offline)";
            }
          }
        }
      }

      if (QuantumRunner.Default && ToggleOn.activeSelf) {
        // realtimeSinceStartup is wall-clock; it keeps advancing while Unity is paused, unlike Time.unscaledTime.
        var now = Time.realtimeSinceStartup;
        var refreshUi = now >= _nextUiUpdateTime;

        if (QuantumRunner.Default.IsRunning) {
          var gameInstance = QuantumRunner.Default.Game;

          // Sampling runs every frame so SmoothedValue averages stay accurate.
          if (gameInstance.Session.FramePredicted != null) {
            _prediction ??= new SmoothedValue(Mathf.CeilToInt(gameInstance.Session.SessionConfig.UpdateFPS * SmoothingTimeSec));
            _prediction.Push(gameInstance.Session.FramePredicted.Number - gameInstance.Session.FrameVerified.Number);

            _verifiedFrames ??= new SmoothedValue(Mathf.CeilToInt(gameInstance.Session.SessionConfig.UpdateFPS * SmoothingTimeSec));
            _verifiedDeltaPrevFrame = _verifiedDeltaPrevFrame < gameInstance.Session.SessionConfig.UpdateFPS ? gameInstance.Session.SessionConfig.UpdateFPS : _verifiedDeltaPrevFrame;
            _verifiedFrames.Push(gameInstance.Session.FrameVerified.Number - _verifiedDeltaPrevFrame);
            _verifiedDeltaPrevFrame = gameInstance.Session.FrameVerified.Number;
          }

          _predictedFrames ??= new SmoothedValue(Mathf.CeilToInt(gameInstance.Session.SessionConfig.UpdateFPS * SmoothingTimeSec));
          _predictedFrames.Push(gameInstance.Session.PredictedFrames);

          _simulateTime ??= new SmoothedValue(Mathf.CeilToInt(gameInstance.Session.SessionConfig.UpdateFPS * SmoothingTimeSec));
          _simulateTime.Push((float)gameInstance.Session.Stats.UpdateTime);

          // Frame numbers refresh at the simulation tick rate — driven by value change, not the UI throttle.
          if (gameInstance.Session.FramePredicted != null) {
            var verifiedNumber = gameInstance.Session.FrameVerified.Number;
            if (verifiedNumber != _lastShownFrameVerified) {
              FrameVerified.text = verifiedNumber.ToString();
              _lastShownFrameVerified = verifiedNumber;
            }
            var predictedNumber = gameInstance.Session.FramePredicted.Number;
            if (predictedNumber != _lastShownFramePredicted) {
              FramePredicted.text = predictedNumber.ToString();
              _lastShownFramePredicted = predictedNumber;
            }
          }

          // Remaining text assignments are throttled to UiRefreshIntervalSec.
          if (refreshUi) {
            if (gameInstance.Session.FramePredicted != null) {
              Predicted.text = _prediction.Average.ToString("0.00", CultureInfo.InvariantCulture);
              VerifiedFrames.text = _verifiedFrames.Average.ToString("0.00", CultureInfo.InvariantCulture) + " (" + _verifiedFrames.Min.ToString(CultureInfo.InvariantCulture) + "-" + _verifiedFrames.Max.ToString(CultureInfo.InvariantCulture) + ")";
            }

            PredictedFrames.text = _predictedFrames.Average.ToString("0.00", CultureInfo.InvariantCulture) + " (" + _predictedFrames.Min.ToString(CultureInfo.InvariantCulture) + "-" + _predictedFrames.Max.ToString(CultureInfo.InvariantCulture) + ")";
            SimulateTime.text = (_simulateTime.Average * 1000).ToString("0.##", CultureInfo.InvariantCulture) + " ms";
            NetworkPing.text = gameInstance.Session.Stats.Ping.ToString();
            InputOffset.text = gameInstance.Session.Stats.Offset.ToString();

            if (gameInstance.Session.IsStalling) {
              SimulationState.text = "Stalling";
              SimulationState.color = Color.red;
            } else {
              SimulationState.text = "Running";
              SimulationState.color = Color.green;
            }
          }
        }

        if (QuantumRunner.Default.NetworkClient != null && QuantumRunner.Default.NetworkClient.IsConnected) {
          if (_networkTimer == null) {
            _networkTimer = Stopwatch.StartNew();
          }

          if (refreshUi) {
            Region.text = QuantumRunner.Default.NetworkClient.CurrentRegion;
          }

          if (UseCurrentBandwidth) {
            // Already self-throttled to ~1 Hz by the deltaTime > 1 gate.
            var deltaTime = _networkTimer.Elapsed.TotalSeconds - _lastTime;
            if (deltaTime > 1) {
              if (_snapshotDelta != null) {
                var snapShotDelta = QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.ToDelta(_snapshotDelta);
                NetworkIn.text = FormatBandwidth(snapShotDelta.BytesIn / (double)snapShotDelta.DeltaTime * 1000d);
                NetworkOut.text = FormatBandwidth(snapShotDelta.BytesOut / (double)snapShotDelta.DeltaTime * 1000d);
              }

              _snapshotDelta = QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.ToSnapshot();
              _lastTime = _networkTimer.Elapsed.TotalSeconds;
            }
          } else if (refreshUi) {
            NetworkIn.text = FormatBandwidth(QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.BytesIn / _networkTimer.Elapsed.TotalSeconds);
            NetworkOut.text = FormatBandwidth(QuantumRunner.Default.NetworkClient.RealtimePeer.Stats.BytesOut / _networkTimer.Elapsed.TotalSeconds);
          }
        }

        if (refreshUi) {
          _nextUiUpdateTime = now + UiRefreshIntervalSec;
        }
      } else {
        _networkTimer = null;
      }
    }

    void SetState(bool state) {
      ToggleOn.SetActive(state);
      ToggleOff.SetActive(!state);
    }

    /// <summary>
    /// Toggle the stats window.
    /// </summary>
    public void Toggle() {
      ToggleOn.SetActive(!ToggleOn.activeSelf);
      ToggleOff.SetActive(!ToggleOff.activeSelf);
    }

    /// <summary>
    /// Find or load the stats windows and enable it.
    /// </summary>
    public static void Show() {
      GetObject().SetState(true);
    }

    /// <summary>
    /// Find or load the stats windows and disable it.
    /// </summary>
    public static void Hide() {
      GetObject().SetState(false);
    }

    /// <summary>
    /// Find or create the stats window.
    /// </summary>
    /// <returns>The stats window object</returns>
    public static QuantumStats GetObject() {
      QuantumStats stats;

      // find existing or create new
      if (!(stats = FindAnyObjectByType<QuantumStats>())) {
        stats = Instantiate(UnityEngine.Resources.Load<QuantumStats>(nameof(QuantumStats)));
      }

      return stats;
    }

    static string FormatFrame(int frame, bool isStalling) {
      if (isStalling) {
        return "<b><color=#FF9794>" + frame.ToString() + "</color></b>";
      }
      return "<b>" + frame.ToString() + "</b>";
    }

    static string FormatPing(int ping) {
      if (ping <= 50) {
        return "<b><color=#B6FFD3>" + ping.ToString() + "</color></b>";
      } else if (ping <= 100) {
        return "<b><color=#F9FFB6>" + ping.ToString() + "</color></b>";
      } else if (ping <= 150) {
        return "<b><color=#FFDAB6>" + ping.ToString() + "</color></b>";
      }

      return "<b><color=#FF9794>" + ping.ToString() + "</color></b>";
    }

    static readonly string[] BytesPerSecondUnits = { "B/s", "KB/s", "MB/s", "GB/s" };

    static string FormatBandwidth(double byteCount) {
      if (byteCount <= 0) {
        return "0 B/s";
      }

      var bytes = Math.Abs((long)byteCount);
      var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
      var num = Math.Round(bytes / Math.Pow(1024, place), 1);

      return (Math.Sign(byteCount) * num).ToString("0.0", CultureInfo.InvariantCulture) + " " + BytesPerSecondUnits[Math.Min(place, BytesPerSecondUnits.Length - 1)];
    }

    private class SmoothedValue {
      RingBuffer<float> _ringBuffer;
      float _sum;
      float _avg;
      float _min;
      float _max;

      public SmoothedValue(int bufferSize) {
        _ringBuffer = new RingBuffer<float>(bufferSize);
      }

      public float Min => _min;
      public float Max => _max;
      public float Average => _avg;

      public void Push(float value) {
        // PeekBack(0) is the oldest item — the one PushFront will evict when the buffer is full.
        var wasFull = _ringBuffer.IsFull;
        var evicted = wasFull ? _ringBuffer.PeekBack(0) : 0f;

        _ringBuffer.PushFront(value);

        _sum += value;
        if (wasFull) {
          _sum -= evicted;
        }
        _avg = _sum / _ringBuffer.Size;

        if (_ringBuffer.Size == 1) {
          _min = value;
          _max = value;
        } else if (wasFull && (evicted == _min || evicted == _max)) {
          // Only path that needs to walk the buffer: an extreme may have been evicted.
          RecomputeMinMax();
        } else {
          if (value < _min) _min = value;
          if (value > _max) _max = value;
        }
      }

      void RecomputeMinMax() {
        var min = float.MaxValue;
        var max = float.MinValue;
        var iterator = _ringBuffer.GetIterator();
        while (iterator.MoveNext()) {
          var v = iterator.Current;
          if (v < min) min = v;
          if (v > max) max = v;
        }
        _min = min;
        _max = max;
      }
    }
  }
}