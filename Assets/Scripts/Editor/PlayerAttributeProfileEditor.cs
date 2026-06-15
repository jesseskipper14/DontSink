#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Survival.Attributes;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerAttributeProfile))]
public sealed class PlayerAttributeProfileEditor : Editor
{
    // Change these if your serialized field names differ.
    private const string BaseValuesFieldName = "baseValues";
    private const string AttributeFieldName = "attribute";
    private const string ValueFieldName = "value";

    private SerializedProperty _baseValues;
    private string _search = string.Empty;

    private void OnEnable()
    {
        _baseValues = serializedObject.FindProperty(BaseValuesFieldName);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();

        if (_baseValues == null || !_baseValues.isArray)
        {
            EditorGUILayout.HelpBox(
                $"Could not find serialized array/list field '{BaseValuesFieldName}'. " +
                $"Either rename the constant in PlayerAttributeProfileEditor or check PlayerAttributeProfile.",
                MessageType.Error);

            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawToolbar();
        DrawProfileTable();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromScriptableObject((ScriptableObject)target);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sync Missing Attributes", GUILayout.Height(24)))
                SyncMissingAttributes();

            if (GUILayout.Button("Remove Duplicates", GUILayout.Height(24)))
                RemoveDuplicateAttributes();
        }

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Search", GUILayout.Width(50));
            _search = EditorGUILayout.TextField(_search);

            if (GUILayout.Button("Clear", GUILayout.Width(55)))
                _search = string.Empty;
        }

        EditorGUILayout.Space(6);

        EditorGUILayout.HelpBox(
            "This profile is the base-value source for player attributes. " +
            "Buffs modify these values at runtime. Use Sync Missing Attributes after adding new PlayerAttributeId enum values.",
            MessageType.Info);
    }

    private void DrawProfileTable()
    {
        List<Row> rows = BuildRows();
        rows.Sort((a, b) => a.enumIndex.CompareTo(b.enumIndex));

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("Attribute", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Value", EditorStyles.boldLabel, GUILayout.Width(110));
        }

        string currentGroup = null;
        bool drewAny = false;

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];

            if (!MatchesSearch(row.rawName, row.niceName))
                continue;

            SerializedProperty element = _baseValues.GetArrayElementAtIndex(row.arrayIndex);
            SerializedProperty attributeProp = element.FindPropertyRelative(AttributeFieldName);
            SerializedProperty valueProp = element.FindPropertyRelative(ValueFieldName);

            if (attributeProp == null || valueProp == null)
                continue;

            string group = GetGroupName(row.rawName);
            if (group != currentGroup)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                currentGroup = group;
            }

            DrawRow(row, attributeProp, valueProp);
            drewAny = true;
        }

        if (!drewAny)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox("No attributes match the current search.", MessageType.None);
        }
    }

    private void DrawRow(Row row, SerializedProperty attributeProp, SerializedProperty valueProp)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);

        float valueWidth = 110f;
        float gap = 8f;

        Rect labelRect = new Rect(
            rect.x,
            rect.y + 1f,
            rect.width - valueWidth - gap,
            EditorGUIUtility.singleLineHeight);

        Rect valueRect = new Rect(
            rect.xMax - valueWidth,
            rect.y + 1f,
            valueWidth,
            EditorGUIUtility.singleLineHeight);

        EditorGUI.LabelField(labelRect, row.niceName);
        valueProp.floatValue = EditorGUI.FloatField(valueRect, valueProp.floatValue);
    }

    private List<Row> BuildRows()
    {
        List<Row> rows = new();

        for (int i = 0; i < _baseValues.arraySize; i++)
        {
            SerializedProperty element = _baseValues.GetArrayElementAtIndex(i);
            SerializedProperty attributeProp = element.FindPropertyRelative(AttributeFieldName);
            SerializedProperty valueProp = element.FindPropertyRelative(ValueFieldName);

            if (attributeProp == null || valueProp == null)
                continue;

            string rawName = GetEnumRawName(attributeProp);
            string niceName = ObjectNames.NicifyVariableName(rawName);

            rows.Add(new Row
            {
                arrayIndex = i,
                enumIndex = attributeProp.enumValueIndex,
                rawName = rawName,
                niceName = niceName
            });
        }

        return rows;
    }

    private void SyncMissingAttributes()
    {
        serializedObject.Update();

        HashSet<int> existingEnumIndexes = new();

        for (int i = 0; i < _baseValues.arraySize; i++)
        {
            SerializedProperty element = _baseValues.GetArrayElementAtIndex(i);
            SerializedProperty attributeProp = element.FindPropertyRelative(AttributeFieldName);

            if (attributeProp == null)
                continue;

            existingEnumIndexes.Add(attributeProp.enumValueIndex);
        }

        string[] enumNames = Enum.GetNames(typeof(PlayerAttributeId));
        int added = 0;

        for (int enumIndex = 0; enumIndex < enumNames.Length; enumIndex++)
        {
            string enumName = enumNames[enumIndex];

            if (ShouldSkipEnum(enumName))
                continue;

            if (existingEnumIndexes.Contains(enumIndex))
                continue;

            int newIndex = _baseValues.arraySize;
            _baseValues.InsertArrayElementAtIndex(newIndex);

            SerializedProperty element = _baseValues.GetArrayElementAtIndex(newIndex);
            SerializedProperty attributeProp = element.FindPropertyRelative(AttributeFieldName);
            SerializedProperty valueProp = element.FindPropertyRelative(ValueFieldName);

            if (attributeProp != null)
                attributeProp.enumValueIndex = enumIndex;

            if (valueProp != null)
                valueProp.floatValue = GetDefaultValue(enumName);

            existingEnumIndexes.Add(enumIndex);
            added++;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);

        Debug.Log($"[PlayerAttributeProfileEditor] Added {added} missing attributes to '{target.name}'.", target);
    }

    private void RemoveDuplicateAttributes()
    {
        serializedObject.Update();

        HashSet<int> seen = new();
        int removed = 0;

        for (int i = _baseValues.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty element = _baseValues.GetArrayElementAtIndex(i);
            SerializedProperty attributeProp = element.FindPropertyRelative(AttributeFieldName);

            if (attributeProp == null)
                continue;

            int enumIndex = attributeProp.enumValueIndex;

            if (seen.Contains(enumIndex))
            {
                _baseValues.DeleteArrayElementAtIndex(i);
                removed++;
                continue;
            }

            seen.Add(enumIndex);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);

        Debug.Log($"[PlayerAttributeProfileEditor] Removed {removed} duplicate attributes from '{target.name}'.", target);
    }

    private bool MatchesSearch(string rawName, string niceName)
    {
        if (string.IsNullOrWhiteSpace(_search))
            return true;

        string s = _search.Trim();

        return rawName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
               niceName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetEnumRawName(SerializedProperty enumProperty)
    {
        if (enumProperty == null)
            return "Unknown";

        string[] names = enumProperty.enumNames;
        int index = enumProperty.enumValueIndex;

        if (names == null || index < 0 || index >= names.Length)
            return "Unknown";

        return names[index];
    }

    private static bool ShouldSkipEnum(string enumName)
    {
        return enumName is
            "None" or
            "Invalid" or
            "Unset";
    }

    private static string GetGroupName(string rawName)
    {
        if (rawName.StartsWith("Exertion", StringComparison.OrdinalIgnoreCase))
            return "Exertion / Energy";

        if (rawName.StartsWith("Walk", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("Run", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("Jump", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("Move", StringComparison.OrdinalIgnoreCase))
            return "Movement";

        if (rawName.StartsWith("Swim", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("Dive", StringComparison.OrdinalIgnoreCase))
            return "Swimming";

        if (rawName.Contains("Air", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Oxygen", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Lung", StringComparison.OrdinalIgnoreCase))
            return "Air / Oxygen";

        if (rawName.Contains("Trade", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Buy", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Sell", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Charisma", StringComparison.OrdinalIgnoreCase))
            return "Trade / Social";

        if (rawName.Contains("Repair", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Salvage", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Craft", StringComparison.OrdinalIgnoreCase))
            return "Work / Utility";

        if (rawName.Contains("Pilot", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Helm", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Engine", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Pump", StringComparison.OrdinalIgnoreCase))
            return "Boat Handling";

        if (rawName.StartsWith("CharacterUpright", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("CharacterMovementLean", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("CharacterLean", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("CharacterSprintLean", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("CharacterWading", StringComparison.OrdinalIgnoreCase) ||
            rawName.StartsWith("CharacterSwimming", StringComparison.OrdinalIgnoreCase) ||
            rawName.Contains("Balance", StringComparison.OrdinalIgnoreCase))
            return "Balance / Upright";

        return "Other";
    }

    private static float GetDefaultValue(string enumName)
    {
        // Defaults only matter for newly-added missing attributes.
        // Existing values are left untouched.

        return enumName switch
        {
            // Exertion / energy defaults
            "ExertionEnergyMax" => 100f,

            "ExertionRestCeiling" => 0.08f,
            "ExertionWalkCeiling" => 0.45f,
            "ExertionSprintCeiling" => 0.98f,
            "ExertionSwimCeiling" => 0.75f,
            "ExertionSprintSwimCeiling" => 1.00f,
            "ExertionDiveCeilingBonus" => 0.08f,

            "ExertionRestApproachRate" => 2.0f,
            "ExertionActivityApproachRate" => 0.8f,
            "ExertionSprintApproachRate" => 1.2f,
            "ExertionSwimApproachRate" => 1.0f,
            "ExertionSprintSwimApproachRate" => 1.6f,
            "ExertionTreadApproachRate" => 1.0f,

            "ExertionDrainThreshold" => 0.70f,
            "ExertionBaseDrainPerSecond" => 4f,
            "ExertionDrainPower" => 2.0f,
            "ExertionRegenPerSecond" => 3f,
            "ExertionRegenThreshold" => 0.40f,
            "ExertionRestingRegenBonus" => 2f,
            "ExertionLandRegenBonus" => 1f,

            "ExertionLowEnergyThreshold" => 0.20f,
            "ExertionAuthorityAtLowThreshold" => 0.65f,
            "ExertionAuthorityAtZero" => 0.30f,

            // Balance / upright defaults
            "CharacterUprightTargetAngleOffsetDeg" => 0f,

            "CharacterUprightStrength" => 40f,
            "CharacterUprightDamping" => 8f,
            "CharacterUprightMaxTorque" => 20f,
            "CharacterUprightDeadZoneDeg" => 0f,

            "CharacterMovementLeanMaxDeg" => 6f,
            "CharacterLeanSmoothSpeed" => 10f,
            "CharacterSprintLeanMultiplier" => 2f,
            "CharacterLeanMinHorizontalSpeed" => 1f,
            "CharacterLeanFullHorizontalSpeed" => 2.5f,

            "CharacterWadingTorqueMultiplier" => 0.65f,
            "CharacterSwimmingTorqueMultiplier" => 0.15f,
            "CharacterUprightHeldMultiplier" => 1.5f,

            // Unknown new attributes default to 0 so they are obviously untuned.
            _ => 0f
        };
    }

    private struct Row
    {
        public int arrayIndex;
        public int enumIndex;
        public string rawName;
        public string niceName;
    }
}

#endif