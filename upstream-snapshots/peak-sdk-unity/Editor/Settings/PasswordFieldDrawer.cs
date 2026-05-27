using UnityEngine;
using UnityEditor;
using Peak.Settings;

namespace Peak.Editor.Settings
{
    /// <summary>
    /// Custom property drawer for PasswordFieldAttribute
    /// Displays password fields with masking and optional partial preview
    /// </summary>
    [CustomPropertyDrawer(typeof(PasswordFieldAttribute))]
    public class PasswordFieldDrawer : PropertyDrawer
    {
        private bool _showPassword = false;
        private const float ButtonWidth = 60f;
        private const float Spacing = 5f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use PasswordField with string fields only.");
                return;
            }

            var passwordAttribute = (PasswordFieldAttribute)attribute;

            EditorGUI.BeginProperty(position, label, property);

            // Split the position for field and button
            var fieldRect = new Rect(position.x, position.y, position.width - ButtonWidth - Spacing, position.height);
            var buttonRect = new Rect(position.x + position.width - ButtonWidth, position.y, ButtonWidth, position.height);

            // Label
            EditorGUI.LabelField(new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height), label);

            // Adjust field rect to account for label
            fieldRect.x += EditorGUIUtility.labelWidth;
            fieldRect.width -= EditorGUIUtility.labelWidth;

            // Password field
            if (_showPassword)
            {
                // Show actual password
                property.stringValue = EditorGUI.TextField(fieldRect, property.stringValue);
            }
            else
            {
                // Show masked field
                var maskedValue = GetMaskedValue(property.stringValue, passwordAttribute);
                var placeholder = string.IsNullOrEmpty(property.stringValue) ? passwordAttribute.Placeholder : maskedValue;

                GUI.enabled = false;
                EditorGUI.TextField(fieldRect, placeholder);
                GUI.enabled = true;

                // Handle input via password field when not showing
                if (Event.current.type == EventType.MouseDown && fieldRect.Contains(Event.current.mousePosition))
                {
                    OpenPasswordInputDialog(property, passwordAttribute);
                    Event.current.Use();
                }
            }

            // Toggle button
            var buttonContent = _showPassword ? "Hide" : "Show";
            if (GUI.Button(buttonRect, buttonContent))
            {
                _showPassword = !_showPassword;
            }

            EditorGUI.EndProperty();
        }

        private string GetMaskedValue(string value, PasswordFieldAttribute passwordAttribute)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (passwordAttribute.ShowPartialPreview && value.Length > 4)
            {
                var maskedPart = new string('*', value.Length - 4);
                var previewPart = value.Substring(value.Length - 4);
                return maskedPart + previewPart;
            }
            else
            {
                return new string('*', Mathf.Max(8, value.Length));
            }
        }

        private void OpenPasswordInputDialog(SerializedProperty property, PasswordFieldAttribute passwordAttribute)
        {
            var currentValue = property.stringValue;
            var title = $"Enter {property.displayName}";
            var message = "Please enter the password:";

            // Simple input dialog for password
            var newValue = EditorInputDialog.Show(title, message, currentValue, true);
            if (newValue != null)
            {
                property.stringValue = newValue;
                property.serializedObject.ApplyModifiedProperties();
            }
        }
    }

    /// <summary>
    /// Simple input dialog utility
    /// </summary>
    public static class EditorInputDialog
    {
        public static string Show(string title, string message, string defaultValue = "", bool isPassword = false)
        {
            var result = defaultValue;
            var dialogResult = false;

            // Create a simple popup window
            var window = EditorWindow.CreateInstance<PasswordInputWindow>();
            window.titleContent = new GUIContent(title);
            window.position = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 75, 400, 150);
            window.Initialize(message, defaultValue, isPassword, (value, confirmed) =>
            {
                result = value;
                dialogResult = confirmed;
            });

            window.ShowModal();

            return dialogResult ? result : null;
        }
    }

    /// <summary>
    /// Simple password input window
    /// </summary>
    public class PasswordInputWindow : EditorWindow
    {
        private string _message;
        private string _inputValue;
        private bool _isPassword;
        private System.Action<string, bool> _onComplete;

        public void Initialize(string message, string defaultValue, bool isPassword, System.Action<string, bool> onComplete)
        {
            _message = message;
            _inputValue = defaultValue;
            _isPassword = isPassword;
            _onComplete = onComplete;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);

            GUI.SetNextControlName("PasswordInput");
            if (_isPassword)
            {
                _inputValue = EditorGUILayout.PasswordField("Password:", _inputValue);
            }
            else
            {
                _inputValue = EditorGUILayout.TextField("Value:", _inputValue);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("OK", GUILayout.Width(60)))
            {
                _onComplete?.Invoke(_inputValue, true);
                Close();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
            {
                _onComplete?.Invoke(_inputValue, false);
                Close();
            }

            EditorGUILayout.EndHorizontal();

            // Focus on input field
            if (Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("PasswordInput");
            }

            // Handle Enter key
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                _onComplete?.Invoke(_inputValue, true);
                Close();
                Event.current.Use();
            }
        }
    }
}