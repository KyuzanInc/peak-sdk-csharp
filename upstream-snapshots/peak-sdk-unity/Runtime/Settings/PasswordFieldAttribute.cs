using System;
using UnityEngine;

namespace Peak.Settings
{
    /// <summary>
    /// Attribute to mark string fields as password fields that should be masked in the Inspector
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class PasswordFieldAttribute : PropertyAttribute
    {
        /// <summary>
        /// Whether to show a partial preview of the password (last 4 characters)
        /// </summary>
        public bool ShowPartialPreview { get; set; } = true;

        /// <summary>
        /// Custom placeholder text to show when field is empty
        /// </summary>
        public string Placeholder { get; set; } = "Enter password...";

        public PasswordFieldAttribute()
        {
        }

        public PasswordFieldAttribute(bool showPartialPreview)
        {
            ShowPartialPreview = showPartialPreview;
        }

        public PasswordFieldAttribute(string placeholder)
        {
            Placeholder = placeholder;
        }
    }
}