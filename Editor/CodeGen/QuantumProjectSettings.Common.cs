// merged ProjectSettings

#region IQuantumProjectSettings.cs

namespace Quantum.Editor {
  using System.IO;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Marker interface for Photon project settings types managed by <see cref="QuantumProjectSettings"/>.
  /// Implementing types are <see cref="ScriptableObject"/>s persisted as JSON under <c>ProjectSettings/</c>.
  /// All members have default implementations and only need overriding to customize behaviour.
  /// </summary>
  public interface IQuantumProjectSettings {
    /// <summary>
    /// Persists this instance to <paramref name="path"/>. Defaults to pretty-printed JSON.
    /// </summary>
    void SaveInternal(string path) {
      QuantumProjectSettings.SaveDefault(path, this);
    }

    /// <summary>
    /// Overwrites this instance with the content read from <paramref name="path"/>.
    /// </summary>
    void LoadInternal(string path) {
      QuantumProjectSettings.LoadDefault(path, this);
    }

    /// <summary>
    /// Called when no backing file exists yet, allowing settings to be seeded from a legacy source.
    /// </summary>
    /// <returns><see langword="true"/> if migration produced data worth saving, otherwise <see langword="false"/>.</returns>
    bool Migrate() {
      return false;
    }

    /// <summary>
    /// Draws extra controls below the auto-generated fields in the settings provider GUI.
    /// </summary>
    void OnSettingsGUI(SerializedObject serializedObject) {
    }
  }
}

#endregion


#region QuantumProjectSettings.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;
  
  /// <summary>
  /// Loads, saves and exposes settings types marked with <see cref="QuantumProjectSettingsAttribute"/>
  /// as entries in the project settings window. Each settings type is backed by a JSON file under
  /// <c>ProjectSettings/</c> and editable through an auto-generated <see cref="SettingsProvider"/>.
  /// </summary>
  public static class QuantumProjectSettings {
    /// <summary>
    /// Returns the cached instance of <typeparamref name="T"/>, loading it from its backing JSON file
    /// (or migrating/creating a default) if needed.
    /// </summary>
    public static T GetInstance<T>() where T : ScriptableObject, IQuantumProjectSettings {
      return QuantumProjectSettings<T>.Instance;
    }

    /// <summary>
    /// Writes the current instance of <typeparamref name="T"/> to its backing JSON file.
    /// </summary>
    public static void Save<T>() where T : ScriptableObject, IQuantumProjectSettings {
      QuantumProjectSettings<T>.Save();
    }

    /// <summary>
    /// Default <see cref="IQuantumProjectSettings.LoadInternal"/> implementation: overwrites
    /// <paramref name="settings"/> with the JSON content read from <paramref name="path"/>.
    /// </summary>
    public static void LoadDefault(string path, IQuantumProjectSettings settings) {
      JsonUtility.FromJsonOverwrite(File.ReadAllText(path), settings);
    }

    /// <summary>
    /// Default <see cref="IQuantumProjectSettings.SaveInternal"/> implementation: writes
    /// <paramref name="settings"/> as pretty-printed JSON to <paramref name="path"/>.
    /// </summary>
    public static void SaveDefault(string path, IQuantumProjectSettings settings) {
      File.WriteAllText(path, JsonUtility.ToJson(settings, prettyPrint: true));
    }

    /// <summary>
    /// Discovers all types marked with <see cref="QuantumProjectSettingsAttribute"/> and creates
    /// a project-scope <see cref="SettingsProvider"/> for each.
    /// </summary>
    [SettingsProviderGroup]
    static SettingsProvider[] Create() {
      var providers = new List<SettingsProvider>();

      var types = TypeCache.GetTypesWithAttribute<QuantumProjectSettingsAttribute>()
        .Select(type => (type, attr: type.GetCustomAttribute<QuantumProjectSettingsAttribute>()))
        .ToList();

      foreach (var (type, attr) in types) {
        IQuantumProjectSettings instance = null;
        SerializedObject serializedObject = null;
        Action saveMethod = null;
        Action refreshMethod = null;

        providers.Add(new SettingsProvider(attr.MenuPath, SettingsScope.Project) {
          keywords = BuildKeywords(type),
          activateHandler = (_, _) => {
            var wrapperType = typeof(QuantumProjectSettings<>).MakeGenericType(type);
            
            var prop = wrapperType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                       ?? throw new InvalidOperationException($"Expected to have Instance property for {type.FullName}");

            instance = (IQuantumProjectSettings)prop.GetValue(null)
                       ?? throw new InvalidOperationException($"Expected Instance to not return null for {type.FullName}");

            saveMethod = (Action)wrapperType.GetMethod("Save", BindingFlags.Public | BindingFlags.Static)!.CreateDelegate(typeof(Action));
            refreshMethod = (Action)wrapperType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Static)!.CreateDelegate(typeof(Action));
            
            serializedObject = new SerializedObject((ScriptableObject)instance);
          },
          deactivateHandler = () => {
            serializedObject?.Dispose();
            serializedObject = null;
            instance = null;
          },
          guiHandler = _ => {
            if (serializedObject == null) {
              return;
            }

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250;
            EditorGUI.indentLevel++;
            try {
              refreshMethod();
              serializedObject.Update();
              EditorGUI.BeginChangeCheck();
              var prop = serializedObject.GetIterator();
              if (prop.NextVisible(true)) {
                do {
                  if (prop.name == "m_Script") {
                    continue;
                  }

                  EditorGUILayout.PropertyField(prop, true);
                } while (prop.NextVisible(false));
              }

              instance.OnSettingsGUI(serializedObject);
              if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                saveMethod?.Invoke();
              }
            } finally {
              EditorGUI.indentLevel--;
              EditorGUIUtility.labelWidth = labelWidth;
            }
          },
        });
      }

      return providers.ToArray();
    }

    /// <summary>
    /// Collects searchable keywords from the serialized fields of <paramref name="type"/> and its base types
    /// so the settings entry can be found through the project settings search box.
    /// </summary>
    static HashSet<string> BuildKeywords(Type type) {
      const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
      var keywords = new HashSet<string>();
      for (var t = type; t != null && t != typeof(object); t = t.BaseType) {
        foreach (var field in t.GetFields(flags)) {
          if (field.IsNotSerialized) {
            continue;
          }

          if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) {
            continue;
          }

          keywords.Add(ObjectNames.NicifyVariableName(field.Name).ToLowerInvariant());
        }
      }

      return keywords;
    }

  }
  
  /// <summary>
  /// Per-type cache that holds the live <typeparamref name="T"/> instance and keeps it in sync with the
  /// backing <c>ProjectSettings/{TypeName}.json</c> file, reloading it whenever the file changes on disk.
  /// </summary>
  static class QuantumProjectSettings<T> where T : ScriptableObject, IQuantumProjectSettings {
    static T _instance;
    static DateTime _lastWriteTime;
    static readonly string FilePath = $"ProjectSettings/{typeof(T).Name}.json";

    /// <summary>
    /// The cached instance. Reloaded from <see cref="FilePath"/> when the file's last write time changes;
    /// when the file does not exist, a default instance is created and optionally migrated.
    /// </summary>
    public static T Instance {
      get {
        if (_instance && File.GetLastWriteTimeUtc(FilePath) == _lastWriteTime) {
          return _instance;
        }

        if (!_instance) {
          _instance = ScriptableObject.CreateInstance<T>();
          _instance.hideFlags = HideFlags.DontSave;
        }

        try {
          if (File.Exists(FilePath)) {
            QuantumEditorLog.TraceImport($"Trying to load {typeof(T).FullName} from {FilePath}");
            _lastWriteTime = File.GetLastWriteTimeUtc(FilePath);
            _instance.LoadInternal(FilePath);
          } else {
            if (_instance.Migrate()) {
              QuantumEditorLog.TraceImport($"Migrating {typeof(T).FullName} to {FilePath}, did not exist");
              Save();
            } else {
              QuantumEditorLog.TraceImport($"Not loading {typeof(T).FullName} from {FilePath}, does not exist");
            }
          }
        } catch (Exception ex) {
          QuantumEditorLog.Exception($"Failed to load {typeof(T).FullName} from {FilePath}", ex);
        }

        return _instance;
      }
    }

    /// <summary>
    /// Writes the current instance to <see cref="FilePath"/>, creating the directory if needed.
    /// </summary>
    public static void Save() {
      _lastWriteTime = File.GetLastWriteTimeUtc(FilePath);
      Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
      _instance.SaveInternal(FilePath);
    }

    /// <summary>
    /// Touches <see cref="Instance"/> to trigger a reload if the backing file has changed on disk.
    /// </summary>
    public static void Refresh() {
      _ = Instance;
    }
  }
}

#endregion


#region QuantumProjectSettingsAttribute.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;

  /// <summary>
  /// Marks an <see cref="IQuantumProjectSettings"/> type so that <see cref="QuantumProjectSettings"/>
  /// exposes it as an entry in the project settings window.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  public class QuantumProjectSettingsAttribute : Attribute {
    /// <param name="menuPath">The path of the entry in the project settings window, e.g. <c>"Project/Photon/Foo"</c>.</param>
    public QuantumProjectSettingsAttribute(string menuPath) {
      MenuPath = menuPath;
    }

    /// <summary>
    /// The path of the entry in the project settings window.
    /// </summary>
    public string MenuPath {
      get;
    }
  }
}

#endregion

