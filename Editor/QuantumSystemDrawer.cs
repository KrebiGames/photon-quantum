using Quantum;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using UnityEditor;
using UnityEngine;

public class QuantumSystemDrawer : OdinValueDrawer<SerializableType<SystemBase>> {
	protected override void DrawPropertyLayout(GUIContent label) {
		SirenixEditorGUI.BeginHorizontalPropertyLayout(label);

		this.CallNextDrawer(null);

		if (SirenixEditorGUI.IconButton(EditorIcons.MagnifyingGlass)) {
			var value = this.ValueEntry.SmartValue;
			var type = value.Value;

			if (type != null) {
				var guids = AssetDatabase.FindAssets("t:MonoScript");

				for (int i = 0; i < guids.Length; i++) {
					var path = AssetDatabase.GUIDToAssetPath(guids[i]);
					var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

					if (script != null && script.GetClass() == type) {
						Selection.activeObject = script;
						EditorGUIUtility.PingObject(script);
						break;
					}
				}
			}
		}

		SirenixEditorGUI.EndHorizontalPropertyLayout();
	}
}