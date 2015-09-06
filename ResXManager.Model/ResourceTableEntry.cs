﻿namespace tomenglertde.ResXManager.Model
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using tomenglertde.ResXManager.Infrastructure;
    using tomenglertde.ResXManager.Model.Properties;

    using TomsToolbox.Desktop;

    /// <summary>
    /// Represents one entry in the resource table.
    /// </summary>
    public class ResourceTableEntry : ObservableObject
    {
        private const string InvariantKey = "@Invariant";
        private readonly ResourceEntity _owner;
        private readonly IDictionary<CultureKey, ResourceLanguage> _languages;
        private readonly ResourceLanguage _neutralLanguage;
        private readonly ResourceTableValues<bool> _fileExists;
        private readonly ResourceTableValues<string> _errors;

        private string _key;
        private ResourceTableValues<string> _values;
        private ResourceTableValues<string> _comments;
        private IList<CodeReference> _codeReferences;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceTableEntry" /> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="key">The resource key.</param>
        /// <param name="languages">The localized values.</param>
        internal ResourceTableEntry(ResourceEntity owner, string key, IDictionary<CultureKey, ResourceLanguage> languages)
        {
            Contract.Requires(owner != null);
            Contract.Requires(!string.IsNullOrEmpty(key));
            Contract.Requires(languages != null);
            Contract.Requires(languages.Any());

            _owner = owner;
            _key = key;
            _languages = languages;

            _values = new ResourceTableValues<string>(_languages, lang => lang.GetValue(_key), (lang, value) => lang.SetValue(_key, value));
            _values.ValueChanged += Values_ValueChanged;

            _comments = new ResourceTableValues<string>(_languages, lang => lang.GetComment(_key), (lang, value) => lang.SetComment(_key, value));
            _comments.ValueChanged += Comments_ValueChanged;

            _fileExists = new ResourceTableValues<bool>(_languages, lang => true, (lang, value) => false);
            _errors = new ResourceTableValues<string>(_languages, GetErrors, (lang, value) => false);

            Contract.Assume(languages.Any());
            _neutralLanguage = languages.First().Value;
            Contract.Assume(_neutralLanguage != null);
            _neutralLanguage.IsNeutralLanguage = true;
        }

        private void ResetTableValues()
        {
            _values.ValueChanged -= Values_ValueChanged;
            _values = new ResourceTableValues<string>(_languages, lang => lang.GetValue(_key), (lang, value) => lang.SetValue(_key, value));
            _values.ValueChanged += Values_ValueChanged;

            _comments.ValueChanged -= Comments_ValueChanged;
            _comments = new ResourceTableValues<string>(_languages, lang => lang.GetComment(_key), (lang, value) => lang.SetComment(_key, value));
            _comments.ValueChanged += Comments_ValueChanged;
        }

        public ResourceEntity Owner
        {
            get
            {
                Contract.Ensures(Contract.Result<ResourceEntity>() != null);
                return _owner;
            }
        }

        /// <summary>
        /// Gets the key of the resource.
        /// </summary>
        public string Key
        {
            get
            {
                Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));

                return _key;
            }
            set
            {
                Contract.Requires(!string.IsNullOrEmpty(value));

                if (_key == value)
                    return;

                var resourceLanguages = _languages.Values;

                if (resourceLanguages.Any(language => language.KeyExists(value)) || !resourceLanguages.All(language => language.CanChange()))
                {
                    Dispatcher.BeginInvoke((Action)(() => OnPropertyChanged("Key")));
                    throw new InvalidOperationException("Key already exists: " + value);
                }

                foreach (var language in resourceLanguages)
                {
                    Contract.Assume(language != null);
                    language.RenameKey(_key, value);
                }

                _key = value;

                ResetTableValues();
                OnPropertyChanged("Key");
            }
        }

        /// <summary>
        /// Gets or sets the comment of the neutral language.
        /// </summary>
        public string Comment
        {
            get
            {
                Contract.Ensures(Contract.Result<string>() != null);
                return _neutralLanguage.GetComment(Key) ?? string.Empty;
            }
            set
            {
                _neutralLanguage.SetComment(Key, value);
                OnPropertyChanged(() => Comment);
            }
        }

        /// <summary>
        /// Gets the localized values.
        /// </summary>
        public ResourceTableValues<string> Values
        {
            get
            {
                Contract.Ensures(Contract.Result<ResourceTableValues<string>>() != null);
                return _values;
            }
        }

        /// <summary>
        /// Gets the localized comments.
        /// </summary>
        [PropertyDependency("Comment")]
        public ResourceTableValues<string> Comments
        {
            get
            {
                Contract.Ensures(Contract.Result<ResourceTableValues<string>>() != null);
                return _comments;
            }
        }

        [PropertyDependency("Values")]
        public ResourceTableValues<bool> FileExists
        {
            get
            {
                Contract.Ensures(Contract.Result<ResourceTableValues<bool>>() != null);
                return _fileExists;
            }
        }

        [PropertyDependency("Values")]
        public ResourceTableValues<string> Errors
        {
            get
            {
                Contract.Ensures(Contract.Result<ResourceTableValues<string>>() != null);
                return _errors;
            }
        }

        [PropertyDependency("Comment")]
        public bool IsInvariant
        {
            get
            {
                return Comment.IndexOf(InvariantKey, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            set
            {
                if (value)
                {
                    if (!IsInvariant)
                    {
                        Comment += InvariantKey;
                    }
                }
                else
                {
                    var comment = Comment;
                    int index;

                    while ((index = comment.IndexOf(InvariantKey, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        Contract.Assume((index + InvariantKey.Length) <= comment.Length);
                        comment = comment.Remove(index, InvariantKey.Length);
                    }

                    Comment = comment;
                }
            }
        }

        [PropertyDependency("Values", "IsInvariant")]
        public bool HasAnyStringFormatParameterMismatches
        {
            get
            {
                return !IsInvariant && HasStringFormatParameterMismatches(_values.Select(lang => lang.GetValue(_key)));
            }
        }

        public IList<CodeReference> CodeReferences
        {
            get
            {
                return _codeReferences;
            }
            internal set
            {
                SetProperty(ref _codeReferences, value, () => CodeReferences);
            }
        }

        public bool CanEdit(CultureInfo culture)
        {
            return _owner.CanEdit(culture);
        }

        public void Refresh()
        {
            OnPropertyChanged(() => Values);
            OnPropertyChanged(() => Comment);
        }

        public bool HasStringFormatParameterMismatches(IEnumerable<CultureKey> cultures)
        {
            Contract.Requires(cultures != null);

            return HasStringFormatParameterMismatches(cultures.Select(lang => _values.GetValue(lang)));
        }

        private void Values_ValueChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(() => Values);
        }

        private void Comments_ValueChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(() => Comment);
        }

        private string GetErrors(ResourceLanguage arg)
        {
            if (arg.IsNeutralLanguage)
                return null;

            var value = arg.GetValue(_key);
            if (string.IsNullOrEmpty(value))
                return null;

            var neutralValue = _neutralLanguage.GetValue(_key);
            if (string.IsNullOrEmpty(neutralValue))
                return null;

            if (HasStringFormatParameterMismatches(neutralValue, value))
                return Resources.StringFormatParameterMismatchError;

            return null;
        }

        private static bool HasStringFormatParameterMismatches(params string[] values)
        {
            return HasStringFormatParameterMismatches((IEnumerable<string>)values);
        }

        private static bool HasStringFormatParameterMismatches(IEnumerable<string> values)
        {
            Contract.Requires(values != null);

            values = values.Where(value => !string.IsNullOrEmpty(value)).ToArray();

            if (!values.Any())
                return false;
            
            return values.Select(GetStringFormatFlags)
                .Distinct()
                .Count() > 1;
        }

        private static readonly Regex _stringFormatParameterPattern = new Regex(@"\{(\d+)(,\d+)?(:\S+)?\}");

        private static long GetStringFormatFlags(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            return _stringFormatParameterPattern
                .Matches(value)
                .Cast<Match>()
                .Aggregate(0L, (a, match) => a | (1L << int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)));
        }

        class Comparer : IEqualityComparer<ResourceTableEntry>
        {
            public static readonly Comparer Default = new Comparer();

            public bool Equals(ResourceTableEntry x, ResourceTableEntry y)
            {
                if (ReferenceEquals(x, y))
                    return true;
                if (ReferenceEquals(x, null))
                    return false;
                if (ReferenceEquals(y, null))
                    return false;

                return x.Owner.Equals(y.Owner) && x.Key.Equals(y.Key);
            }

            public int GetHashCode(ResourceTableEntry obj)
            {
                if (obj == null)
                    throw new ArgumentNullException("obj");

                return obj.Owner.GetHashCode() + obj.Key.GetHashCode();
            }
        }

        public static IEqualityComparer<ResourceTableEntry> EqualityComparer
        {
            get
            {
                Contract.Ensures(Contract.Result<IEqualityComparer<ResourceTableEntry>>() != null);

                return Comparer.Default;
            }
        }

        [ContractInvariantMethod]
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Required for code contracts.")]
        private void ObjectInvariant()
        {
            Contract.Invariant(!string.IsNullOrEmpty(_key));
            Contract.Invariant(_values != null);
            Contract.Invariant(_comments != null);
            Contract.Invariant(_fileExists != null);
            Contract.Invariant(_neutralLanguage != null);
            Contract.Invariant(_owner != null);
            Contract.Invariant(_languages != null);
        }
    }
}
